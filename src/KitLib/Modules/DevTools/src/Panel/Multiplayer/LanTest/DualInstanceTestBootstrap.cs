using KitLib.Multiplayer.Cheat;
using KitLib.Settings;

namespace KitLib.Multiplayer.LanTest;

/// <summary>Defaults for same-machine dual-instance LAN multiplayer testing.</summary>
internal static class DualInstanceTestBootstrap {
    /// <summary>
    /// LAN / pseudo-coop dev sessions need the rail even when settings have Normal run disabled.
    /// In-memory only — does not write settings.json.
    /// </summary>
    public static void EnsureMultiplayerDevActive(string reason) {
        if (KitLibState.NormalRunMode == NormalRunMode.Disabled) {
            KitLibState.NormalRunMode = NormalRunMode.DevPanel;
            KitLog.Info("LanTest", $"DevMode active for multiplayer dev session ({reason}).");
        }

        EnsureCheatsEnabled(reason);
    }

    public static void EnsureCheatsEnabled(string reason) {
        if (!KitLibProcessScope.IsDualInstanceActive())
            return;

        KitLibState.NormalRunMode = NormalRunMode.Cheat;

        if (!SettingsStore.Current.MultiplayerCheatOptIn) {
            SettingsStore.SetMultiplayerCheatOptIn(true);
            KitLog.Info("DualInstance", $"Multiplayer cheat opt-in enabled ({reason}).");
        }
        else {
            MpCheatSession.SetLocalOptIn(true);
        }

        KitLog.Info("DualInstance", $"Cheat mode enabled for dual-instance test ({reason}).");
        KitLog.Warn("DualInstance", $"Use different game profiles per window (profile1 + profile2) to avoid save conflicts.");
    }
}
