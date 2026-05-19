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
            if (!File.Exists(FilePath)) {
                ApplyNormalRunModeFromSettings();
                return;
            }
            var json = File.ReadAllText(FilePath);
            Current = JsonSerializer.Deserialize<DevModeSettings>(json, JsonOpts) ?? new();
            ApplyRailLayoutDefaults();
            ApplyNormalRunModeFromSettings();
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"SettingsStore load failed: {ex.Message}");
            Current = new();
            ApplyNormalRunModeFromSettings();
        }
    }

    public static void SetNormalRunMode(NormalRunMode mode) {
        Current.NormalRunMode = mode.ToString();
        DevModeState.NormalRunMode = mode;
        Save();
    }

    public static void SetCombatStatsMpOverlayEnabled(bool enabled) {
        Current.CombatStatsMpOverlayEnabled = enabled;
        Save();
    }

    public static void SetCombatStatsMpOverlayPosition(float x, float y) {
        Current.CombatStatsMpOverlayPosX = x;
        Current.CombatStatsMpOverlayPosY = y;
        Save();
    }

    public static void SetCombatStatsMonsterIntentOverlayEnabled(bool enabled) {
        Current.CombatStatsMonsterIntentOverlayEnabled = enabled;
        Save();
    }

    public static void SetCombatStatsMonsterIntentOverlayPosition(float x, float y) {
        Current.CombatStatsMonsterIntentOverlayPosX = x;
        Current.CombatStatsMonsterIntentOverlayPosY = y;
        Save();
    }

    private static void ApplyNormalRunModeFromSettings() {
        DevModeState.NormalRunMode = ParseNormalRunMode(Current.NormalRunMode);
    }

    private static NormalRunMode ParseNormalRunMode(string? value) {
        if (Enum.TryParse(value, ignoreCase: true, out NormalRunMode mode))
            return mode;
        return NormalRunMode.DevPanel;
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

