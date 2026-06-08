using KitLib.AI.AutoPlay;
using KitLib.Multiplayer.Cheat;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Client AFK: local player accepts host-enqueued combat actions only.</summary>
internal static class MpAiTeammateAfkClient {
    public static bool IsSessionEnabled =>
        KitLibInstanceRegistry.IsDualInstanceActive()
            ? KitLibInstance.SessionLan.MpAiTeammateAfkClient
            : SettingsStore.Current.MpAiTeammateAfkClient;

    public static bool IsEnabled =>
        IsSessionEnabled
        && MpCheatSession.InMultiplayerRun
        && !MpCheatSession.IsHost;

    public static void SetSessionEnabled(bool enabled) {
        if (enabled
            && !MpCheatSession.IsHost
            && !KitLibInstanceRegistry.IsDualInstanceActive()
            && !SettingsStore.Current.MpAiTeammateDriveLiveEnet) {
            MainFile.Logger.Warn(
                "[MpAiTeammate] AFK client enabled but host DriveLiveEnet is off in shared settings — "
                + "host must apply LAN host preset or desync is likely.");
        }

        if (KitLibInstanceRegistry.IsDualInstanceActive())
            KitLibInstance.SessionLan.MpAiTeammateAfkClient = enabled;
        else {
            SettingsStore.Current.MpAiTeammateAfkClient = enabled;
            SettingsStore.Save();
        }

        if (enabled) AiPlayModule.Instance.StopLoop();
        MainFile.Logger.Info(
            $"[MpAiTeammate] AFK client {(enabled ? "enabled" : "disabled")} pid={KitLibInstance.ProcessId} dual={KitLibInstanceRegistry.IsDualInstanceActive()}");
    }

    public static bool ShouldBlockLocalCombatInput(Player? player) {
        if (!IsEnabled || player == null) return false;
        return LocalContext.IsMe(player);
    }
}
