using System.Threading.Tasks;
using HarmonyLib;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;

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

// Replace AfterAddedToRoom entirely: skip the time-limit power and set ∞ HP display.
// BattleFriendV3 only calls base (no-op) then applies BattlewornDummyTimeLimitPower —
// we drop that power and set HpDisplay to match WaterfallGiant's infinite-HP pattern.
[HarmonyPatch(typeof(BattleFriendV3), nameof(BattleFriendV3.AfterAddedToRoom))]
internal static class BattleFriendV3AfterAddedPatch {
    static bool Prefix(BattleFriendV3 __instance, ref Task __result) {
        if (__instance.Creature != null)
            __instance.Creature.HpDisplay = HpDisplay.InfiniteWithoutNumbers;
        __result = Task.CompletedTask;
        return false;
    }
}

// Mirror WaterfallGiant's infinite-HP pattern: 999_999_999 HP.
[HarmonyPatch(typeof(BattleFriendV3), nameof(BattleFriendV3.MinInitialHp), MethodType.Getter)]
internal static class BattleFriendV3MinHpPatch {
    static void Postfix(ref int __result) => __result = 999_999_999;
}

[HarmonyPatch(typeof(BattleFriendV3), nameof(BattleFriendV3.MaxInitialHp), MethodType.Getter)]
internal static class BattleFriendV3MaxHpPatch {
    static void Postfix(ref int __result) => __result = 999_999_999;
}
