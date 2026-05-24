using DevMode.Multiplayer.Cheat;
using DevMode.Multiplayer.PseudoCoop;
using DevMode.Settings;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.LanTest;

/// <summary>Defaults for same-machine dual-instance LAN multiplayer testing.</summary>
internal static class DualInstanceTestBootstrap {
    public static void EnsureCheatsEnabled(string reason) {
        if (!DevModeInstanceRegistry.IsDualInstanceActive())
            return;

        DevModeState.NormalRunMode = NormalRunMode.Cheat;

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
        if (!DevModeInstanceRegistry.IsDualInstanceActive()) return;
        if (!MpCheatSession.InMultiplayerRun) return;

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