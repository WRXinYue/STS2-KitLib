using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.Replay.Patches.Replay;

[HarmonyPatch]
static class ReplayInputGuardPatch {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NClickableControl), "OnReleaseHandler")]
    static bool SkipPlayerClicks(NClickableControl __instance) {
        if (!ReplayInputGuard.IsLocked || ReplayInputGuard.IsAutomated)
            return true;
        if (__instance is NCombatCardPile)
            return true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NMapPoint), "OnRelease")]
    static bool SkipMapTravel() {
        if (!ReplayInputGuard.IsLocked || ReplayInputGuard.IsAutomated)
            return true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.TryManualPlay))]
    static bool SkipManualCardPlay(ref bool __result) {
        if (!ReplayInputGuard.IsLocked || CardPlayReplayPatch._dispatching)
            return true;
        __result = false;
        return false;
    }
}
