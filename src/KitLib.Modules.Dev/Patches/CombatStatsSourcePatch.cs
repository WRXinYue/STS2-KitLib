using HarmonyLib;
using KitLib.CombatStats;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KitLib.Patches;

/// <summary>
/// Captures effect sources when combat history entries are written.
/// Patches history methods (not async CreatureCmd) so stack/context is still valid.
/// </summary>
[HarmonyPatch]
internal static class CombatStatsSourcePatch {
    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.BlockGained))]
    static class BlockGainedHistoryPatch {
        static void Prefix(Creature receiver, ValueProp props, CardPlay? cardPlay) {
            if (!KitLibState.IsActive)
                return;

            var source = CombatStatsSourceResolver.ResolveBlock(cardPlay, props, receiver);
            CombatStatsTracker.SetPendingEffectSource(source);
        }
    }

    [HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.PowerReceived))]
    static class PowerReceivedHistoryPatch {
        static void Prefix(PowerModel power, Creature? applier) {
            if (!KitLibState.IsActive)
                return;

            var source = CombatStatsSourceResolver.ResolvePowerApply(power, applier);
            CombatStatsTracker.SetPendingEffectSource(source);
        }
    }
}
