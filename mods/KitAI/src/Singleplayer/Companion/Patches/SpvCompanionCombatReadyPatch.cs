using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;

namespace KitLib.Singleplayer.Companion.Patches;

/// <summary>
/// Vanilla SP skips the all-players-ready gate in <see cref="CombatManager.AllPlayersReadyToEndTurn"/>.
/// When a companion is present, require every living player to ready up (MP-style).
/// </summary>
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.AllPlayersReadyToEndTurn))]
internal static class SpvCompanionAllPlayersReadyToEndTurnPatch {
    [HarmonyPostfix]
    static void Postfix(CombatManager __instance, ref bool __result) {
        if (!SpvCompanionRegistry.HasAny || !SpvCompanionRegistry.IsSingleplayerRun())
            return;

        var state = __instance.DebugOnlyGetState();
        if (state == null || state.CurrentSide != CombatSide.Player) {
            __result = false;
            return;
        }

        var living = state.Players.Where(p => p.Creature.IsAlive).ToList();
        __result = living.Count > 0 && living.All(__instance.IsPlayerReadyToEndTurn);
    }
}
