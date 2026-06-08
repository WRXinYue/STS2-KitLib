using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitLib.Host;

namespace KitLib.Settings;

/// <summary>
/// Loads and saves <see cref="KitLibSettings"/> to <c>settings.json</c> in the DevMode user-data directory.
/// </summary>
public static class SettingsStore {
    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string FilePath => DataPaths.SettingsFile;

    public static KitLibSettings Current { get; private set; } = new();

    public static void Load() {
        for (int attempt = 0; attempt < 3; attempt++) {
            try {
                if (!File.Exists(FilePath)) {
                    Current = new KitLibSettings { RailIntroDismissed = false };
                    ApplyNormalRunModeFromSettings();
                    Save();
                    return;
                }
                var json = ReadSharedText(FilePath);
                Current = JsonSerializer.Deserialize<KitLibSettings>(json, JsonOpts) ?? new();
                ApplyRailLayoutDefaults();
                ApplyLegacyUiKeyMigration();
                ApplyProgressGuardDefaults();
                ApplyHotkeyDefaults();
                ApplyHotkeySettingsMigration();
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
        KitLibCheatOps.SetMultiplayerCheatOptIn?.Invoke(enabled);
        Save();
    }

    public static void SetNormalRunMode(NormalRunMode mode) {
        Current.NormalRunMode = mode.ToString();
        KitLibState.NormalRunMode = mode;
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

    public static void SetHotkeysEnabled(bool enabled) {
        Current.HotkeysEnabled = enabled;
        Save();
    }

    public static HotkeyBinding GetHotkeyBinding(string actionId) =>
        Current.GetHotkey(actionId).Clone();

    public static string? TrySetHotkeyBinding(string actionId, HotkeyBinding binding) {
        var reason = HotkeyBinding.ValidateForAssign(actionId, binding, Current);
        if (reason != null)
            return reason;
        Current.SetHotkey(actionId, binding);
        Save();
        return null;
    }

    public static void ResetHotkeys() {
        HotkeyDefaults.ApplyTo(Current);
        Save();
    }

    public static void SetCombatStatsMpOverlayPosition(float x, float y) {
        if (KitLibHost.IsDualInstanceActive?.Invoke() == true) {
            KitLibInstance.SessionOverlay.MpOverlayPosX = x;
            KitLibInstance.SessionOverlay.MpOverlayPosY = y;
            return;
        }
        Current.CombatStatsMpOverlayPosX = x;
        Current.CombatStatsMpOverlayPosY = y;
        Save();
    }

    public static (float? X, float? Y) GetCombatStatsMpOverlayPosition() {
        if (KitLibHost.IsDualInstanceActive?.Invoke() == true)
            return (KitLibInstance.SessionOverlay.MpOverlayPosX, KitLibInstance.SessionOverlay.MpOverlayPosY);
        return (Current.CombatStatsMpOverlayPosX, Current.CombatStatsMpOverlayPosY);
    }

    public static void SetCombatStatsMonsterIntentOverlayEnabled(bool enabled) {
        Current.CombatStatsMonsterIntentOverlayEnabled = enabled;
        Save();
    }

    public static void SetCombatStatsMonsterIntentOverlayPosition(float x, float y) {
        if (KitLibHost.IsDualInstanceActive?.Invoke() == true) {
            KitLibInstance.SessionOverlay.MonsterIntentOverlayPosX = x;
            KitLibInstance.SessionOverlay.MonsterIntentOverlayPosY = y;
            return;
        }
        Current.CombatStatsMonsterIntentOverlayPosX = x;
        Current.CombatStatsMonsterIntentOverlayPosY = y;
        Save();
    }

    public static (float? X, float? Y) GetCombatStatsMonsterIntentOverlayPosition() {
        if (KitLibHost.IsDualInstanceActive?.Invoke() == true)
            return (KitLibInstance.SessionOverlay.MonsterIntentOverlayPosX,
                KitLibInstance.SessionOverlay.MonsterIntentOverlayPosY);
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
        KitLibState.NormalRunMode = ParseNormalRunMode(Current.NormalRunMode);
    }

    private static NormalRunMode ParseNormalRunMode(string? value) {
        if (Enum.TryParse(value, ignoreCase: true, out NormalRunMode mode))
            return mode;
        return NormalRunMode.DevPanel;
    }

    private static void ApplyRailLayoutDefaults() {
        if (Current.RailLayoutDefaultsVersion >= 1)
            return;
        foreach (var id in KitLibSettings.DefaultHiddenRailTabIds)
            Current.RailHiddenTabIds.Add(id);
        Current.RailLayoutDefaultsVersion = 1;
        Save();
    }

    /// <summary>Remap browser overlay width keys from legacy DevMode* root names to KitLib*.</summary>
    private static void ApplyLegacyUiKeyMigration() {
        if (Current.BrowserPanelWidths.Count == 0)
            return;

        var remapped = false;
        var next = new Dictionary<string, int>(Current.BrowserPanelWidths, StringComparer.Ordinal);
        foreach (var (key, width) in Current.BrowserPanelWidths) {
            if (!key.StartsWith("DevMode", StringComparison.Ordinal))
                continue;
            var newKey = "KitLib" + key["DevMode".Length..];
            if (!next.ContainsKey(newKey))
                next[newKey] = width;
            next.Remove(key);
            remapped = true;
        }

        if (!remapped)
            return;

        Current.BrowserPanelWidths = next;
        Save();
    }

    private static void ApplyProgressGuardDefaults() {
        if (Current.ProgressGuardSettingsVersion >= 1)
            return;
        Current.PromptOnModCharacterProgressLoss = true;
        Current.ProgressGuardSettingsVersion = 1;
        Save();
    }

    private static void ApplyHotkeyDefaults() {
        if (Current.HotkeyToggleRail.KeyCode == 0)
            Current.HotkeyToggleRail = HotkeyDefaults.ToggleRail.Clone();
        if (Current.HotkeyClosePanel.KeyCode == 0)
            Current.HotkeyClosePanel = HotkeyDefaults.ClosePanel.Clone();
        if (Current.HotkeyNextTab.KeyCode == 0)
            Current.HotkeyNextTab = HotkeyDefaults.NextTab.Clone();
        if (Current.HotkeyPrevTab.KeyCode == 0)
            Current.HotkeyPrevTab = HotkeyDefaults.PrevTab.Clone();
        if (Current.HotkeyLockRail.KeyCode == 0)
            Current.HotkeyLockRail = HotkeyDefaults.LockRail.Clone();
        if (Current.HotkeyQuickSave.KeyCode == 0)
            Current.HotkeyQuickSave = HotkeyDefaults.QuickSave.Clone();
        if (Current.HotkeyQuickLoad.KeyCode == 0)
            Current.HotkeyQuickLoad = HotkeyDefaults.QuickLoad.Clone();
        if (Current.HotkeyQuickReplayCombat.KeyCode == 0)
            Current.HotkeyQuickReplayCombat = HotkeyDefaults.QuickReplayCombat.Clone();
        if (Current.HotkeyQuickReplayTurn.KeyCode == 0)
            Current.HotkeyQuickReplayTurn = HotkeyDefaults.QuickReplayTurn.Clone();
    }

    private static void ApplyHotkeySettingsMigration() {
        if (Current.HotkeySettingsVersion < 1) {
            var legacyToggle = new HotkeyBinding { KeyCode = (int)Godot.Key.D, Ctrl = true, Shift = true };
            if (Current.HotkeyToggleRail.EqualsBinding(legacyToggle))
                Current.HotkeyToggleRail = HotkeyDefaults.ToggleRail.Clone();

            foreach (var actionId in HotkeyActionId.All) {
                var binding = Current.GetHotkey(actionId);
                if (actionId == HotkeyActionId.ClosePanel && binding.KeyCode == (int)Godot.Key.Escape)
                    continue;
                if (!HotkeyBinding.UsesGameShortcutKey(binding.Keycode))
                    continue;
                Current.SetHotkey(actionId, HotkeyDefaults.For(actionId));
            }

            Current.HotkeySettingsVersion = 1;
            Save();
        }

        if (Current.HotkeySettingsVersion < 2) {
            if (Current.HotkeyQuickSave.KeyCode == 0)
                Current.HotkeyQuickSave = HotkeyDefaults.QuickSave.Clone();
            if (Current.HotkeyQuickLoad.KeyCode == 0)
                Current.HotkeyQuickLoad = HotkeyDefaults.QuickLoad.Clone();
            Current.HotkeySettingsVersion = 2;
            Save();
        }

        if (Current.HotkeySettingsVersion < 3) {
            if (Current.HotkeyQuickRestartTurn.KeyCode == 0)
                Current.HotkeyQuickRestartTurn = HotkeyDefaults.QuickReplayCombat.Clone();
            Current.HotkeySettingsVersion = 3;
            Save();
        }

        if (Current.HotkeySettingsVersion < 4) {
            if (Current.HotkeyQuickReplayCombat.KeyCode == 0) {
                Current.HotkeyQuickReplayCombat = Current.HotkeyQuickRestartTurn.KeyCode != 0
                    ? Current.HotkeyQuickRestartTurn.Clone()
                    : HotkeyDefaults.QuickReplayCombat.Clone();
            }
            if (Current.HotkeyQuickReplayTurn.KeyCode == 0)
                Current.HotkeyQuickReplayTurn = HotkeyDefaults.QuickReplayTurn.Clone();
            Current.HotkeySettingsVersion = 4;
            Save();
        }
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

    public static void SetPromptOnModCharacterProgressLoss(bool enabled) {
        Current.PromptOnModCharacterProgressLoss = enabled;
        Save();
    }

    public static void SetPromptOnCrashFeedback(bool enabled) {
        Current.PromptOnCrashFeedback = enabled;
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

