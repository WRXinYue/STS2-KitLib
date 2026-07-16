using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace KitLib.CombatStats;

/// <summary>
/// Persists the live combat stats snapshot locally so the browser can catch up after a late connect.
/// </summary>
internal static class CombatStatsLiveBuffer {
    private const int MinPersistIntervalMs = 200;

    private static readonly object Gate = new();

    private static CombatStatsLiveDto? _latest;
    private static string? _latestJson;
    private static long _revision;
    private static DateTime _lastPersistUtc = DateTime.MinValue;

    public static long Revision {
        get {
            lock (Gate)
                return _revision;
        }
    }

    public static CombatStatsLiveDto? Latest {
        get {
            lock (Gate) {
                if (_latest != null)
                    return _latest;
                return TryLoadFromDisk();
            }
        }
    }

    public static string LiveFilePath =>
        Path.Combine(OS.GetUserDataDir(), "mod_data", "KitLib", "combat-stats-live.json");

    public static void ResetForNewCombat() {
        lock (Gate) {
            _latest = null;
            _latestJson = null;
            _revision = 0;
            _lastPersistUtc = DateTime.MinValue;
            try {
                if (File.Exists(LiveFilePath))
                    File.Delete(LiveFilePath);
            }
            catch (Exception ex) {
                KitLog.Warn("CombatStats", $"Live buffer reset failed: {ex.Message}");
            }
        }
    }

    /// <summary>Capture current stats and write to disk. Skips work when unchanged within the throttle window.</summary>
    public static CombatStatsLiveDto Persist(bool force = false) {
        lock (Gate) {
            var live = CombatStatsExport.CaptureLive();
            string json = CombatStatsExport.ToJson(live);

            bool throttled = !force
                && json == _latestJson
                && (DateTime.UtcNow - _lastPersistUtc).TotalMilliseconds < MinPersistIntervalMs;
            if (throttled && _latest != null)
                return _latest;

            _latest = live;
            _latestJson = json;
            _revision++;
            WriteAtomic(json);
            _lastPersistUtc = DateTime.UtcNow;
            return live;
        }
    }

    public static bool TryReadJson(out string json) {
        lock (Gate) {
            if (!string.IsNullOrEmpty(_latestJson)) {
                json = _latestJson;
                return true;
            }

            try {
                if (!File.Exists(LiveFilePath)) {
                    json = "";
                    return false;
                }

                json = File.ReadAllText(LiveFilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) {
                    json = "";
                    return false;
                }

                _latestJson = json;
                _latest = JsonSerializer.Deserialize<CombatStatsLiveDto>(json, CombatStatsExport.JsonOptions);
                if (_revision == 0)
                    _revision = 1;
                return true;
            }
            catch (Exception ex) {
                KitLog.Warn("CombatStats", $"Live buffer read failed: {ex.Message}");
                json = "";
                return false;
            }
        }
    }

    private static CombatStatsLiveDto? TryLoadFromDisk() {
        if (!TryReadJson(out string json) || string.IsNullOrWhiteSpace(json))
            return null;

        try {
            return _latest ?? JsonSerializer.Deserialize<CombatStatsLiveDto>(json, CombatStatsExport.JsonOptions);
        }
        catch (Exception ex) {
            KitLog.Warn("CombatStats", $"Live buffer parse failed: {ex.Message}");
            return null;
        }
    }

    private static void WriteAtomic(string json) {
        try {
            string path = LiveFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
        catch (Exception ex) {
            KitLog.Warn("CombatStats", $"Live buffer write failed: {ex.Message}");
        }
    }
}
