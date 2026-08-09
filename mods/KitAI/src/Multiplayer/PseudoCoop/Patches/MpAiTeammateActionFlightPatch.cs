using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

/// <summary>Track in-flight queue actions so end-turn is not signaled while a card is still resolving.</summary>
internal static class MpAiTeammateActionFlightPatch {
    internal static void OnActionEnd(GameAction action) {
        if (action == null) return;
        var netId = PseudoCoopActionQueue.ResolvePlayerNetId(action);
        if (netId == 0) return;

        PseudoCoopActionQueue.ClearInFlight(netId);
        if (action is PlayCardAction or EndPlayerTurnAction)
            MpAiTeammateHost.NotifyCombatActionFinished(netId);
    }
}

[HarmonyPatch(typeof(ActionExecutor), "AfterActionFinished")]
internal static class MpAiTeammateAfterActionFinishedPatch {
    [HarmonyPostfix]
    static void Postfix(GameAction action) => MpAiTeammateActionFlightPatch.OnActionEnd(action);
}
