using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

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
            MainFile.Logger.Info($"[LanTest] DevMode active for multiplayer dev session ({reason}).");
        }

        EnsureCheatsEnabled(reason);
    }

    public static void EnsureCheatsEnabled(string reason) {
        if (!KitLibInstanceRegistry.IsDualInstanceActive())
            return;

        KitLibState.NormalRunMode = NormalRunMode.Cheat;

        if (!SettingsStore.Current.MultiplayerCheatOptIn) {
            SettingsStore.SetMultiplayerCheatOptIn(true);
            MainFile.Logger.Info($"[DualInstance] Multiplayer cheat opt-in enabled ({reason}).");
        }
        else {
            MpCheatSession.SetLocalOptIn(true);
        }

        MainFile.Logger.Info($"[DualInstance] Cheat mode enabled for dual-instance test ({reason}).");
        MainFile.Logger.Warn(
            "[DualInstance] Use different game profiles per window (profile1 + profile2) to avoid save conflicts.");
    }

    /// <summary>Dual-instance LAN: auto-apply host-drive + client AFK presets so half-config cannot desync.</summary>
    internal static void TryAutoLanPresetsOnLaunch() {
        if (!KitLibInstanceRegistry.IsDualInstanceActive()) return;

        var netType = RunManager.Instance?.NetService?.Type;
        if (netType == NetGameType.Host) {
            if (SettingsStore.Current.MpAiTeammateEnabled
                && SettingsStore.Current.MpAiTeammateDriveLiveEnet) {
                MainFile.Logger.Info("[DualInstance] LAN host preset already active.");
                return;
            }

            PseudoCoopBootstrap.ApplyLanHostPreset();
            MainFile.Logger.Info("[DualInstance] Auto-applied LAN host preset (dual-instance).");
        }
        else if (netType == NetGameType.Client) {
            if (MpAiTeammateAfkClient.IsSessionEnabled) {
                MainFile.Logger.Info("[DualInstance] LAN client AFK already active.");
                return;
            }

            PseudoCoopBootstrap.ApplyLanClientPreset();
            MainFile.Logger.Info("[DualInstance] Auto-applied LAN client AFK preset (dual-instance).");
        }
    }
}