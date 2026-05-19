using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevMode.Settings;

/// <summary>
/// Loads and saves <see cref="DevModeSettings"/> to <c>settings.json</c> in the DevMode user-data directory.
/// </summary>
public static class SettingsStore {
    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string FilePath => DataPaths.SettingsFile;

    public static DevModeSettings Current { get; private set; } = new();

    public static void Load() {
        try {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            Current = JsonSerializer.Deserialize<DevModeSettings>(json, JsonOpts) ?? new();
            ApplyRailLayoutDefaults();
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"SettingsStore load failed: {ex.Message}");
            Current = new();
        }
    }

    private static void ApplyRailLayoutDefaults() {
        if (Current.RailLayoutDefaultsVersion >= 1)
            return;
        RailTabPreferences.ApplyDefaultHiddenTabs(Current);
        Current.RailLayoutDefaultsVersion = 1;
        Save();
    }

    public static void Save() {
        try {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, JsonOpts));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"SettingsStore save failed: {ex.Message}");
        }
    }
}

