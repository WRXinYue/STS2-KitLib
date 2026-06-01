using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevMode.Multiplayer.Cheat;

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
        for (int attempt = 0; attempt < 3; attempt++) {
            try {
                if (!File.Exists(FilePath)) {
                    Current = new DevModeSettings { RailIntroDismissed = false };
                    ApplyNormalRunModeFromSettings();
                    Save();
                    return;
                }
                var json = ReadSharedText(FilePath);
                Current = JsonSerializer.Deserialize<DevModeSettings>(json, JsonOpts) ?? new();
                ApplyRailLayoutDefaults();
                ApplyNormalRunModeFromSettings();
                return;
            }
            catch (IOException) when (attempt < 2) {
                System.Threading.Thread.Sleep(40);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"SettingsStore load failed: {ex.Message}");
                Current = new();
                ApplyNormalRunModeFromSettings();
                return;
            }
        }
    }

    public static void SetMultiplayerCheatOptIn(bool enabled) {
        Current.MultiplayerCheatOptIn = enabled;
        MpCheatSession.SetLocalOptIn(enabled);
        Save();
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

    public static void SetGameContextPaneEnabled(bool enabled) {
        Current.GameContextPaneEnabled = enabled;
        Save();
    }

    public static void SetCombatStatsMpOverlayPosition(float x, float y) {
        if (DevModeInstanceRegistry.IsDualInstanceActive()) {
            DevModeInstance.SessionOverlay.MpOverlayPosX = x;
            DevModeInstance.SessionOverlay.MpOverlayPosY = y;
            return;
        }
        Current.CombatStatsMpOverlayPosX = x;
        Current.CombatStatsMpOverlayPosY = y;
        Save();
    }

    public static (float? X, float? Y) GetCombatStatsMpOverlayPosition() {
        if (DevModeInstanceRegistry.IsDualInstanceActive())
            return (DevModeInstance.SessionOverlay.MpOverlayPosX, DevModeInstance.SessionOverlay.MpOverlayPosY);
        return (Current.CombatStatsMpOverlayPosX, Current.CombatStatsMpOverlayPosY);
    }

    public static void SetCombatStatsMonsterIntentOverlayEnabled(bool enabled) {
        Current.CombatStatsMonsterIntentOverlayEnabled = enabled;
        Save();
    }

    public static void SetCombatStatsMonsterIntentOverlayPosition(float x, float y) {
        if (DevModeInstanceRegistry.IsDualInstanceActive()) {
            DevModeInstance.SessionOverlay.MonsterIntentOverlayPosX = x;
            DevModeInstance.SessionOverlay.MonsterIntentOverlayPosY = y;
            return;
        }
        Current.CombatStatsMonsterIntentOverlayPosX = x;
        Current.CombatStatsMonsterIntentOverlayPosY = y;
        Save();
    }

    public static (float? X, float? Y) GetCombatStatsMonsterIntentOverlayPosition() {
        if (DevModeInstanceRegistry.IsDualInstanceActive())
            return (DevModeInstance.SessionOverlay.MonsterIntentOverlayPosX,
                DevModeInstance.SessionOverlay.MonsterIntentOverlayPosY);
        return (Current.CombatStatsMonsterIntentOverlayPosX, Current.CombatStatsMonsterIntentOverlayPosY);
    }

    public static bool ShouldShowRailIntroHint()
        => Current.RailIntroDismissed == false;

    public static void MarkRailIntroDismissed() {
        if (Current.RailIntroDismissed == true)
            return;
        Current.RailIntroDismissed = true;
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

    public static void SetShowHiddenCards(bool enabled) {
        Current.ShowHiddenCards = enabled;
        Save();
    }

    public static void SetAutoBackupProgressOnModChange(bool enabled) {
        Current.AutoBackupProgressOnModChange = enabled;
        Save();
    }

    public static void SetWarnOnRemovedModProgressResidue(bool enabled) {
        Current.WarnOnRemovedModProgressResidue = enabled;
        Save();
    }

    public static void Save() {
        for (int attempt = 0; attempt < 3; attempt++) {
            try {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(Current, JsonOpts));
                File.Move(tmp, FilePath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 2) {
                System.Threading.Thread.Sleep(40);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"SettingsStore save failed: {ex.Message}");
                return;
            }
        }
    }

    private static string ReadSharedText(string path) {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

