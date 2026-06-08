using System.Reflection;
using KitLib.Multiplayer.Cheat;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

/// <summary>
/// Host RequestEnqueue uses host NetId as actionOwnerId; host-driven peers need message.playerId = action.OwnerId
/// so both peers execute EndPlayerTurnAction / PlayCardAction for the correct player.
/// </summary>
[HarmonyPatch(typeof(ActionQueueSynchronizer), nameof(ActionQueueSynchronizer.RequestEnqueue))]
internal static class PseudoCoopHostEnqueuePatch {
    static readonly MethodInfo EnqueueActionMethod =
        AccessTools.Method(typeof(ActionQueueSynchronizer), "EnqueueAction")!;

    [HarmonyPrefix]
    static bool Prefix(GameAction action, ActionQueueSynchronizer __instance) {
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return true;
        if (!MpCheatSession.InMultiplayerRun) return true;

        var hostNetId = RunManager.Instance.NetService.NetId;
        if (action.OwnerId == hostNetId) return true;
        if (!SimulatedPeerRegistry.IsHostDrivenPeer(action.OwnerId)) return true;

        if (action.ActionType == GameActionType.CombatPlayPhaseOnly
            && __instance.CombatState == ActionSynchronizerCombatState.NotPlayPhase
            && action is not ReadyToBeginEnemyTurnAction)
            return true;

        EnqueueActionMethod.Invoke(__instance, [action, action.OwnerId]);
        return false;
    }
}
