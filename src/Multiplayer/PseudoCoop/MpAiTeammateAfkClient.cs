using DevMode.AI.AutoPlay;
using DevMode.Multiplayer.Cheat;
using DevMode.Settings;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;

namespace DevMode.Multiplayer.PseudoCoop;

/// <summary>Client AFK: local player accepts host-enqueued combat actions only.</summary>
internal static class MpAiTeammateAfkClient {
    public static bool IsSessionEnabled =>
        DevModeInstanceRegistry.IsDualInstanceActive()
            ? DevModeInstance.SessionLan.MpAiTeammateAfkClient
            : SettingsStore.Current.MpAiTeammateAfkClient;

    public static bool IsEnabled =>
        IsSessionEnabled
        && MpCheatSession.InMultiplayerRun
        && !MpCheatSession.IsHost;

    public static void SetSessionEnabled(bool enabled) {
        if (enabled
            && !MpCheatSession.IsHost
            && !DevModeInstanceRegistry.IsDualInstanceActive()
            && !SettingsStore.Current.MpAiTeammateDriveLiveEnet) {
            MainFile.Logger.Warn(
                "[MpAiTeammate] AFK client enabled but host DriveLiveEnet is off in shared settings — "
                + "host must apply LAN host preset or desync is likely.");
        }

        if (DevModeInstanceRegistry.IsDualInstanceActive())
            DevModeInstance.SessionLan.MpAiTeammateAfkClient = enabled;
        else {
            SettingsStore.Current.MpAiTeammateAfkClient = enabled;
            SettingsStore.Save();
        }

        if (enabled) AiPlayModule.Instance.StopLoop();
        MainFile.Logger.Info(
            $"[MpAiTeammate] AFK client {(enabled ? "enabled" : "disabled")} pid={DevModeInstance.ProcessId} dual={DevModeInstanceRegistry.IsDualInstanceActive()}");
    }

    public static bool ShouldBlockLocalCombatInput(Player? player) {
        if (!IsEnabled || player == null) return false;
        return LocalContext.IsMe(player);
    }
}
