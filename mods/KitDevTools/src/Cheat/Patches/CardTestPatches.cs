using HarmonyLib;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Patches;

/// <summary>
/// Harmony patches for card testing: when <see cref="CardTestState.BypassResourceCosts"/> is true
/// (automated Test queue), plays skip energy/star spend.
/// </summary>

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.LoseEnergy))]
internal static class FreePlayLoseEnergyPatch {
    static bool Prefix() => !CardTestState.BypassResourceCosts;
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.LoseStars))]
internal static class FreePlayLoseStarsPatch {
    static bool Prefix() => !CardTestState.BypassResourceCosts;
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.HasEnoughResourcesFor))]
internal static class FreePlayHasEnoughPatch {
    static void Postfix(ref bool __result) {
        if (CardTestState.BypassResourceCosts)
            __result = true;
    }
}
