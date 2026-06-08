using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Phantom players join mid-run after <see cref="ActionQueueSet"/> was built with one queue.</summary>
internal static class PseudoCoopActionQueue {
    static readonly Dictionary<ulong, int> InFlightCounts = [];

    static readonly FieldInfo QueuesField =
        AccessTools.Field(typeof(ActionQueueSet), "_actionQueues")!;

    static readonly Type QueueType = AccessTools.Inner(typeof(ActionQueueSet), "ActionQueue")!;

    static readonly FieldInfo OwnerIdField = AccessTools.Field(QueueType, "ownerId")!;
    static readonly FieldInfo ActionsField = AccessTools.Field(QueueType, "actions")!;

    internal static void EnsureQueueForPlayer(Player player) {
        var set = RunManager.Instance?.ActionQueueSet;
        if (set == null) return;

        if (QueuesField.GetValue(set) is not IList queues) return;

        foreach (var q in queues) {
            if ((ulong)OwnerIdField.GetValue(q)! == player.NetId)
                return;
        }

        var queue = Activator.CreateInstance(QueueType)!;
        OwnerIdField.SetValue(queue, player.NetId);
        ActionsField.SetValue(queue, new List<GameAction>());
        queues.Add(queue);

        MainFile.Logger.Info($"[PseudoCoop] Action queue added for netId={player.NetId}.");
    }

    internal static bool HasQueuedActions(ulong netId) {
        var set = RunManager.Instance?.ActionQueueSet;
        if (set == null) return false;
        if (QueuesField.GetValue(set) is not IList queues) return false;

        foreach (var q in queues) {
            if ((ulong)OwnerIdField.GetValue(q)! != netId) continue;
            if (ActionsField.GetValue(q) is IList actions && actions.Count > 0)
                return true;
        }

        return false;
    }

    internal static bool HasQueuedEndTurn(ulong netId) {
        if (!TryGetPlayerQueueActions(netId, out var actions)) return false;
        foreach (GameAction action in actions) {
            if (action is EndPlayerTurnAction)
                return true;
        }

        return false;
    }

    internal static bool HasQueuedReadyToBeginEnemyTurn(ulong netId) {
        if (!TryGetPlayerQueueActions(netId, out var actions)) return false;
        foreach (GameAction action in actions) {
            if (action is ReadyToBeginEnemyTurnAction)
                return true;
        }

        return false;
    }

    internal static bool HasPendingCombatActions(ulong netId) {
        if (HasInFlightAction(netId)) return true;
        if (HasQueuedNonEndTurnActions(netId)) return true;
        return IsRunningPlayerDrivenNonEndTurn(netId);
    }

    static bool HasQueuedNonEndTurnActions(ulong netId) {
        if (!TryGetPlayerQueueActions(netId, out var actions)) return false;
        foreach (GameAction action in actions) {
            if (action is not EndPlayerTurnAction)
                return true;
        }

        return false;
    }

    static bool IsRunningPlayerDrivenNonEndTurn(ulong netId) {
        var running = RunManager.Instance?.ActionExecutor?.CurrentlyRunningAction;
        if (running == null || running is EndPlayerTurnAction) return false;
        if (ResolvePlayerNetId(running) != netId) return false;
        return ActionQueueSet.IsGameActionPlayerDriven(running);
    }

    static bool TryGetPlayerQueueActions(ulong netId, out IList actions) {
        actions = null!;
        var set = RunManager.Instance?.ActionQueueSet;
        if (set == null) return false;
        if (QueuesField.GetValue(set) is not IList queues) return false;

        foreach (var q in queues) {
            if ((ulong)OwnerIdField.GetValue(q)! != netId) continue;
            if (ActionsField.GetValue(q) is IList list) {
                actions = list;
                return true;
            }
        }

        return false;
    }

    internal static void MarkInFlight(ulong netId) {
        InFlightCounts.TryGetValue(netId, out var count);
        InFlightCounts[netId] = count + 1;
    }

    internal static void ClearInFlight(ulong netId) {
        if (!InFlightCounts.TryGetValue(netId, out var count)) return;
        if (count <= 1) InFlightCounts.Remove(netId);
        else InFlightCounts[netId] = count - 1;
    }

    internal static bool HasInFlightAction(ulong netId) =>
        InFlightCounts.TryGetValue(netId, out var count) && count > 0;

    internal static void ClearStaleInFlight(ulong netId) {
        if (HasQueuedNonEndTurnActions(netId)) return;
        if (IsRunningPlayerDrivenNonEndTurn(netId)) return;
        ClearInFlight(netId);
    }

    internal static void ClearInFlightAll() => InFlightCounts.Clear();

    /// <summary>PlayCardAction.OwnerId may be the host queue owner; use the action's player net id.</summary>
    internal static ulong ResolvePlayerNetId(GameAction action) {
        var player = Traverse.Create(action).Field<Player>("_player").Value;
        if (player == null)
            player = Traverse.Create(action).Property<Player>("Player").Value;
        return player?.NetId ?? action.OwnerId;
    }
}
