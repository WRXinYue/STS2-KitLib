using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

internal static class MpAiTeammateCombatActions {
    public static void SignalEndTurn(Player player) {
        if (!CanSignalEndTurn(player)) return;
        EnqueueOrSetReadyForAiTarget(player);
    }

    public static void SignalEndTurnForHostDrivenPeer(Player player) {
        if (!CanSignalEndTurn(player)) return;
        EnqueueOrSetReady(player);
    }

    /// <summary>After host AI is toggled off: clear stale in-flight, then end turn if the queue is idle.</summary>
    public static void ForceSignalEndTurnForHostDrivenPeer(Player player) {
        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm)) return;
        if (cm.IsPlayerReadyToEndTurn(player)) return;
        if (PseudoCoopActionQueue.HasQueuedEndTurn(player.NetId)) return;
        if (PseudoCoopActionQueue.HasPendingCombatActions(player.NetId)) return;

        EnqueueOrSetReady(player);
    }

    public static void EnqueueEndTurn(Player player) {
        if (PseudoCoopActionQueue.HasQueuedEndTurn(player.NetId)) {
            MainFile.Logger.Debug($"[MpAiTeammate] End turn already queued netId={player.NetId}.");
            return;
        }

        if (!CanSignalEndTurn(player)) return;

        var round = CombatManager.Instance?.DebugOnlyGetState()?.RoundNumber ?? 1;
        var action = new EndPlayerTurnAction(player, round);
        RunManager.Instance!.ActionQueueSynchronizer.RequestEnqueue(action);
        MainFile.Logger.Info($"[MpAiTeammate] Enqueued end turn netId={player.NetId} round={round}.");
    }

    static void EnqueueOrSetReady(Player player) {
        if (SimulatedPeerRegistry.ShouldHostRouteCombatEnqueue(player))
            EnqueueEndTurn(player);
        else
            CombatManager.Instance!.SetReadyToEndTurn(player, canBackOut: false);
    }

    static void EnqueueOrSetReadyForAiTarget(Player player) {
        if (SimulatedPeerRegistry.ShouldHostEnqueueCombatAction(player))
            EnqueueEndTurn(player);
        else
            CombatManager.Instance!.SetReadyToEndTurn(player, canBackOut: false);
    }

    static bool CanSignalEndTurn(Player player) {
        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm)) return false;
        if (cm.IsPlayerReadyToEndTurn(player)) return false;
        if (PseudoCoopActionQueue.HasPendingCombatActions(player.NetId)) return false;
        if (PseudoCoopActionQueue.HasQueuedEndTurn(player.NetId)) return false;
        return true;
    }
}
