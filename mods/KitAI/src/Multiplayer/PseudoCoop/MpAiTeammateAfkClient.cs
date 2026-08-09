using KitLib.AI.AutoPlay;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.SyncBot;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Client AFK: local player accepts host-enqueued combat actions only.</summary>
internal static class MpAiTeammateAfkClient {
    public static bool IsSessionEnabled => AiSessionSettings.MpAiTeammateAfkClient;

    public static bool IsEnabled =>
        IsSessionEnabled
        && MpCheatSession.InMultiplayerRun
        && !MpCheatSession.IsHost;

    public static void SetSessionEnabled(bool enabled) {
        if (enabled
            && !MpCheatSession.IsHost
            && !AiSessionSettings.MpAiTeammateDriveLiveEnet) {
            KitLog.Warn("MpAiTeammate",
                "AFK client enabled but host DriveLiveEnet is off — "
                + "host must apply LAN host preset or desync is likely.");
        }

        AiSessionSettings.MpAiTeammateAfkClient = enabled;

        if (enabled) AiPlayModule.Instance.StopLoop();
        KitLog.Info("MpAiTeammate",
            $"AFK client {(enabled ? "enabled" : "disabled")} pid={KitLibInstance.ProcessId}");
    }

    public static bool ShouldBlockLocalCombatInput(Player? player) {
        if (!IsEnabled || player == null) return false;
        return LocalContext.IsMe(player);
    }
}
