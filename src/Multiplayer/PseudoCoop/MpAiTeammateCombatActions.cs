using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.PseudoCoop;

internal static class MpAiTeammateCombatActions {
    public static void SignalEndTurn(Player player) {
        if (SimulatedPeerRegistry.ShouldHostEnqueueCombatAction(player))
            EndTurnForHostDrivenPeer(player);
        else
            CombatManager.Instance?.SetReadyToEndTurn(player, canBackOut: false);
    }

    /// <summary>
    /// Live ENet peers: host-only ready flag (vanilla sync). Phantom/offline: action queue enqueue.
    /// NetEndPlayerTurnAction does not carry the target player; RequestEnqueue desyncs real clients.
    /// </summary>
    static void EndTurnForHostDrivenPeer(Player player) {
        if (SimulatedPeerRegistry.IsLiveEnetPeer(player.NetId)) {
            CombatManager.Instance?.SetReadyToEndTurn(player, canBackOut: false);
            MainFile.Logger.Info($"[MpAiTeammate] Ready end turn (live ENet) netId={player.NetId}.");
            return;
        }

        EnqueueEndTurn(player);
    }

    public static void EnqueueEndTurn(Player player) {
        if (SimulatedPeerRegistry.IsLiveEnetPeer(player.NetId)) {
            MainFile.Logger.Warn(
                $"[MpAiTeammate] EnqueueEndTurn blocked for live ENet netId={player.NetId}; using SetReadyToEndTurn.");
            EndTurnForHostDrivenPeer(player);
            return;
        }

        var round = CombatManager.Instance?.DebugOnlyGetState()?.RoundNumber ?? 1;
        var action = new EndPlayerTurnAction(player, round);
        RunManager.Instance!.ActionQueueSynchronizer.RequestEnqueue(action);
        MainFile.Logger.Info($"[MpAiTeammate] Enqueued end turn netId={player.NetId} round={round}.");
    }
}
