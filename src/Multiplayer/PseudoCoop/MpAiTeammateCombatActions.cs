using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.PseudoCoop;

internal static class MpAiTeammateCombatActions {
    public static void SignalEndTurn(Player player) {
        if (!CanSignalEndTurn(player)) return;

        if (SimulatedPeerRegistry.ShouldHostEnqueueCombatAction(player))
            EnqueueEndTurn(player);
        else
            CombatManager.Instance!.SetReadyToEndTurn(player, canBackOut: false);
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

    static bool CanSignalEndTurn(Player player) {
        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm)) return false;
        if (cm.IsPlayerReadyToEndTurn(player)) return false;
        if (PseudoCoopActionQueue.HasPendingCombatActions(player.NetId)) return false;
        if (PseudoCoopActionQueue.HasQueuedEndTurn(player.NetId)) return false;
        return true;
    }
}
