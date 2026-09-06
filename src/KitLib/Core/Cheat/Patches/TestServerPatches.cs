using HarmonyLib;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Patches;

/// <summary>
/// Harmony patches for the test server: when <see cref="TestServerState.BypassResourceCosts"/> is true
/// (automated Test queue), plays skip energy/star spend.
/// </summary>

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.LoseEnergy))]
internal static class FreePlayLoseEnergyPatch {
    static bool Prefix() => !TestServerState.BypassResourceCosts;
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.LoseStars))]
internal static class FreePlayLoseStarsPatch {
    static bool Prefix() => !TestServerState.BypassResourceCosts;
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.HasEnoughResourcesFor))]
internal static class FreePlayHasEnoughPatch {
    static void Postfix(ref bool __result) {
        if (TestServerState.BypassResourceCosts)
            __result = true;
    }
}
