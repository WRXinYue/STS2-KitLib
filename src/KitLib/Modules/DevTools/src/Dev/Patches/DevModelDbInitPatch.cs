using HarmonyLib;
using KitLib;
using KitLib.CombatStats;
using KitLib.Dev;
using KitLib.EnemyIntent;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Patches;

/// <summary>Retries Dev runtime wiring after core game models are initialized.</summary>
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
internal static class DevModelDbInitPatch {
    [HarmonyPostfix]
    static void Postfix() {
        ModuleBootstrap.Complete();
        CombatStatsTracker.EnsureWired();
        MonsterIntentOverlayTracker.EnsureWired();
        MonsterIntentOverrides.EnsureWired();
    }
}
