using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Play;

internal static class NetCombatCommands {
    internal static bool TryEnqueuePlayCard(Player player, CardModel card, Creature? target) {
        if (!HostDrivenPeers.ShouldHostEnqueueCombatAction(player))
            return false;

        CombatActionQueue.EnsureQueueForPlayer(player);
        var playAction = new PlayCardAction(
            player,
            NetCombatCard.FromModel(card),
            card.Id,
            target?.CombatId);
        RunManager.Instance!.ActionQueueSynchronizer.RequestEnqueue(playAction);
        CombatActionQueue.MarkInFlight(player.NetId);
        return true;
    }

    public static void SignalEndTurn(Player player) {
        if (!CanSignalEndTurn(player)) return;
        EnqueueOrSetReadyForAiTarget(player);
    }

    public static void SignalEndTurnForHostDrivenPeer(Player player) {
        if (!CanSignalEndTurn(player)) return;
        EnqueueOrSetReady(player);
    }

    public static void ForceSignalEndTurnForHostDrivenPeer(Player player) {
        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm)) return;
        if (cm.IsPlayerReadyToEndTurn(player)) return;
        if (CombatActionQueue.HasQueuedEndTurn(player.NetId)) return;
        if (CombatActionQueue.HasPendingCombatActions(player.NetId)) return;

        EnqueueOrSetReady(player);
    }

    public static void EnqueueEndTurn(Player player) {
        if (CombatActionQueue.HasQueuedEndTurn(player.NetId)) {
            KitLog.Debug("NetCombat", $"End turn already queued netId={player.NetId}.");
            return;
        }

        if (!CanSignalEndTurn(player)) return;

        var round = CombatManager.Instance?.DebugOnlyGetState()?.RoundNumber ?? 1;
        var action = new EndPlayerTurnAction(player, round);
        RunManager.Instance!.ActionQueueSynchronizer.RequestEnqueue(action);
        KitLog.Info("NetCombat", $"Enqueued end turn netId={player.NetId} round={round}.");
    }

    public static void SignalReadyToBeginEnemyTurn(Player player) {
        var cm = CombatManager.Instance;
        if (cm is not { IsInProgress: true }) return;
        if (Sts2CombatCompat.IsPlayerReadyToBeginEnemyTurn(cm, player)) return;
        if (CombatActionQueue.HasQueuedReadyToBeginEnemyTurn(player.NetId)) return;

        if (HostDrivenPeers.ShouldHostEnqueueCombatAction(player)) {
            var action = new ReadyToBeginEnemyTurnAction(player);
            RunManager.Instance!.ActionQueueSynchronizer.RequestEnqueue(action);
            KitLog.Info("NetCombat", $"Enqueued ready-to-begin-enemy-turn netId={player.NetId}.");
            return;
        }

        cm.SetReadyToBeginEnemyTurn(player);
        KitLog.Info("NetCombat", $"Ready-to-begin-enemy-turn netId={player.NetId}.");
    }

    static void EnqueueOrSetReady(Player player) {
        if (HostDrivenPeers.ShouldHostRouteCombatEnqueue(player))
            EnqueueEndTurn(player);
        else
            CombatManager.Instance!.SetReadyToEndTurn(player, canBackOut: false);
    }

    static void EnqueueOrSetReadyForAiTarget(Player player) {
        if (HostDrivenPeers.ShouldHostEnqueueCombatAction(player))
            EnqueueEndTurn(player);
        else
            CombatManager.Instance!.SetReadyToEndTurn(player, canBackOut: false);
    }

    static bool CanSignalEndTurn(Player player) {
        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm)) return false;
        if (cm.IsPlayerReadyToEndTurn(player)) return false;
        if (CombatActionQueue.HasPendingCombatActions(player.NetId)) return false;
        if (CombatActionQueue.HasQueuedEndTurn(player.NetId)) return false;
        return true;
    }
}
