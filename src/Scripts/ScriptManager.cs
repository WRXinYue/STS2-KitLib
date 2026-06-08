using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitLib.Hooks;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Scripts;

/// <summary>
/// Central dispatcher for SpireScratch scripts.
/// Scans <c>scripts/</c> directory, watches for file changes, and fires triggers.
/// </summary>
internal static class ScriptManager {
    private static readonly JsonSerializerOptions JsonOpts = new() {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        // Blockly / hand-edited JSON use string enum names ("CombatStart"), not numeric values.
        Converters = { new JsonStringEnumConverter() },
    };

    private static volatile List<LoadedScript> _scripts = new();
    private static readonly object _reloadLock = new();
    private static FileSystemWatcher? _watcher;
    private static bool _initialized;
    private static volatile bool _dirty;

    public static string ScriptsDir { get; private set; } = "";
    public static DateTime LastReloadTime { get; private set; }
    public static string? LastError { get; private set; }
    public static IReadOnlyList<LoadedScript> Scripts => _scripts;

    /// <summary>Monotonically increasing counter, bumped on every Reload().</summary>
    public static int ReloadVersion { get; private set; }

    public sealed class LoadedScript {
        public string FilePath { get; init; } = "";
        public string FileName { get; init; } = "";
        public ScriptEntry Entry { get; init; } = new();
        public string? ParseError { get; init; }
    }

    // ──────── Lifecycle ────────

    public static void Initialize() {
        if (_initialized) return;
        _initialized = true;

        ScriptsDir = DataPaths.ScriptsDir;

        Reload();
        StartWatcher();

        MainFile.Logger.Info($"[ScriptManager] Initialized — {_scripts.Count} script(s) from {ScriptsDir}");
    }

    public static void Shutdown() {
        StopWatcher();
        _scripts = new List<LoadedScript>();
        _initialized = false;
    }

    /// <summary>Mark scripts as needing reload on next game-thread tick.</summary>
    public static void RequestReload() => _dirty = true;

    /// <summary>Call from game thread (e.g. process tick) to apply pending hot-reload.</summary>
    public static void ProcessPendingReload() {
        if (!_dirty) return;
        _dirty = false;
        Reload();
        MainFile.Logger.Info($"[ScriptManager] Hot-reloaded — {_scripts.Count} script(s)");
    }

    // ──────── Fire ────────

    public static void Fire(TriggerType trigger, Player? player) {
        if (!KitLibState.IsActive) return;
        if (_scripts.Count == 0) return;
        if (player == null && !RunContext.TryGetRunAndPlayer(out _, out player)) return;

        if (trigger == TriggerType.CombatStart)
            ScriptVariableStore.Reset();

        foreach (var loaded in _scripts) {
            if (loaded.ParseError != null) continue;
            var entry = loaded.Entry;
            if (!entry.Enabled || entry.Trigger != trigger) continue;

            try {
                if (!ScriptConditionEvaluator.Evaluate(entry.RootCondition, player))
                    continue;

                ScriptActionExecutor.Execute(entry.RootAction, player!);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[Script] Error executing '{entry.Name}' ({trigger}): {ex.Message}");
            }
        }
    }

    // ──────── Load / Reload ────────

    public static void Reload() {
        lock (_reloadLock) {
            var next = new List<LoadedScript>();
            LastError = null;
            LastReloadTime = DateTime.Now;

            if (!Directory.Exists(ScriptsDir)) { _scripts = next; return; }

            try {
                foreach (var file in Directory.GetFiles(ScriptsDir, "*.json")) {
                    try {
                        var json = File.ReadAllText(file);
                        var entry = JsonSerializer.Deserialize<ScriptEntry>(json, JsonOpts);
                        if (entry == null) continue;

                        next.Add(new LoadedScript {
                            FilePath = file,
                            FileName = Path.GetFileName(file),
                            Entry = entry,
                        });
                    }
                    catch (Exception ex) {
                        next.Add(new LoadedScript {
                            FilePath = file,
                            FileName = Path.GetFileName(file),
                            ParseError = ex.Message,
                        });
                        LastError = $"{Path.GetFileName(file)}: {ex.Message}";
                        MainFile.Logger.Warn($"[ScriptManager] Parse error in {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex) {
                LastError = ex.Message;
                MainFile.Logger.Warn($"[ScriptManager] Reload failed: {ex.Message}");
            }

            _scripts = next;
            ReloadVersion++;
        }
    }

    public static void SaveScript(ScriptEntry entry, string fileName) {
        var json = JsonSerializer.Serialize(entry, JsonOpts);
        SaveRaw(fileName, json);
    }

    /// <summary>Write raw JSON content directly to scripts/ (used by WebSocket bridge).</summary>
    public static void SaveRaw(string fileName, string rawJson) {
        try {
            if (!Directory.Exists(ScriptsDir))
                Directory.CreateDirectory(ScriptsDir);

            var safe = Path.GetFileName(fileName);
            if (!safe.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                safe += ".json";

            File.WriteAllText(Path.Combine(ScriptsDir, safe), rawJson);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ScriptManager] Save failed: {ex.Message}");
        }
    }

    public static string? ReadRaw(string fileName) {
        try {
            var path = Path.Combine(ScriptsDir, Path.GetFileName(fileName));
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch { return null; }
    }

    public static void DeleteScript(string fileName) {
        try {
            var path = Path.Combine(ScriptsDir, fileName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ScriptManager] Delete failed: {ex.Message}");
        }
    }

    // ──────── FileSystemWatcher ────────

    private static void StartWatcher() {
        try {
            if (!Directory.Exists(ScriptsDir)) return;

            _watcher = new FileSystemWatcher(ScriptsDir, "*.json") {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };

            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Renamed += (_, _) => _dirty = true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ScriptManager] Watcher setup failed: {ex.Message}");
        }
    }

    private static void StopWatcher() {
        if (_watcher == null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    private static void OnFileEvent(object sender, FileSystemEventArgs e) {
        _dirty = true;
    }
}
