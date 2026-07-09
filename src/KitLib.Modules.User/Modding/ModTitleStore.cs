using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitLib.Abstractions.Modding;

namespace KitLib.Modding;

/// <summary>Per-mod custom display titles under <see cref="DataPaths.ModTitleOverridesFile"/>.</summary>
public static class ModTitleStore {
    private const int MaxTitleLength = 120;

    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static ModTitleFileData _data = new();
    private static bool _loaded;

    public static void Load() {
        _loaded = true;
        try {
            if (!File.Exists(DataPaths.ModTitleOverridesFile)) {
                _data = new ModTitleFileData();
                return;
            }
            _data = JsonSerializer.Deserialize<ModTitleFileData>(File.ReadAllText(DataPaths.ModTitleOverridesFile), JsonOpts)
                ?? new ModTitleFileData();
            _data.Entries ??= new Dictionary<string, ModTitleEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) {
            KitLog.Warn("ModTitle", $"Failed to load mod title overrides: {ex.Message}");
            _data = new ModTitleFileData();
        }
    }

    public static string? GetOverride(string modId, ModEntrySource source) {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(modId))
            return null;
        return _data.Entries.TryGetValue(FormatStorageKey(modId, source), out var entry)
            ? entry.Title
            : null;
    }

    public static string Resolve(string modId, ModEntrySource source, string defaultTitle) {
        var custom = GetOverride(modId, source);
        return string.IsNullOrWhiteSpace(custom) ? defaultTitle : custom;
    }

    public static void Set(string modId, ModEntrySource source, string title, string defaultTitle) {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(modId))
            return;
        var key = FormatStorageKey(modId, source);
        var normalized = NormalizeTitle(title);
        var defaultNormalized = NormalizeTitle(defaultTitle);
        if (string.IsNullOrEmpty(normalized)
            || string.Equals(normalized, defaultNormalized, StringComparison.Ordinal)) {
            if (_data.Entries.Remove(key))
                Persist();
            return;
        }
        if (_data.Entries.TryGetValue(key, out var existing)
            && string.Equals(existing.Title, normalized, StringComparison.Ordinal)) {
            return;
        }
        _data.Entries[key] = new ModTitleEntry {
            Title = normalized,
            UpdatedUtc = DateTimeOffset.UtcNow,
        };
        Persist();
    }

    internal static string FormatStorageKey(string modId, ModEntrySource source)
        => $"{modId.Trim()}|{source}";

    private static string NormalizeTitle(string title) {
        if (string.IsNullOrWhiteSpace(title))
            return "";
        var trimmed = title.Trim();
        return trimmed.Length <= MaxTitleLength ? trimmed : trimmed[..MaxTitleLength];
    }

    private static void EnsureLoaded() {
        if (!_loaded)
            Load();
    }

    private static void Persist() {
        for (int attempt = 0; attempt < 3; attempt++) {
            try {
                Directory.CreateDirectory(DataPaths.BaseDir);
                var path = DataPaths.ModTitleOverridesFile;
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_data, JsonOpts));
                File.Move(tmp, path, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 2) {
                System.Threading.Thread.Sleep(40);
            }
            catch (Exception ex) {
                KitLog.Warn("ModTitle", $"Failed to save mod title overrides: {ex.Message}");
                return;
            }
        }
    }

    private sealed class ModTitleFileData {
        public int Version { get; set; } = 1;
        public Dictionary<string, ModTitleEntry> Entries { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ModTitleEntry {
        public string Title { get; set; } = "";
        public DateTimeOffset? UpdatedUtc { get; set; }
    }
}
