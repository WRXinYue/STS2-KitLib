using HarmonyLib;
using KitLib.Host;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace KitLib.Multiplayer.Play.Patches;

internal static class CombatActionFlight {
    internal static void OnActionEnd(GameAction action) {
        if (action == null) return;
        var netId = CombatActionQueue.ResolvePlayerNetId(action);
        if (netId == 0) return;

        CombatActionQueue.ClearInFlight(netId);
        if (action is PlayCardAction or EndPlayerTurnAction)
            KitLibNetPlayOps.OnCombatActionFinished?.Invoke(netId);
    }
}

[HarmonyPatch(typeof(ActionExecutor), "AfterActionFinished")]
internal static class CombatActionFlightPatch {
    [HarmonyPostfix]
    static void Postfix(GameAction action) => CombatActionFlight.OnActionEnd(action);
}
