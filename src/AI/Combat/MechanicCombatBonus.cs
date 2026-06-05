using System;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat;

/// <summary>Mechanic-index-driven combat bonuses (not per-card id tables).</summary>
internal static class MechanicCombatBonus {
    public static int Score(
        JsonObject snapshot,
        JsonObject card,
        CardMechanicProfile profile,
        JsonArray? hand,
        JsonObject? targetEnemy,
        int energy) {
        var bonus = 0;

        if (profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)) {
            if (CombatTransformSimulator.CountTransformableAttacks(hand) == 0)
                return CombatScoreWeights.UnusableTransformPenalty;

            var gain = CombatTransformSimulator.EstimateDamageGain(hand, card);
            bonus += gain;

            var projected = CombatTransformSimulator.ProjectHandAfterTransform(hand, card);
            var skillCost = card["cost"]?.GetValue<int>() ?? 0;
            var remainingEnergy = Math.Max(0, energy - skillCost);
            bonus += CombatCardStats.EstimateFollowupAttackDamage(projected, remainingEnergy) / 2;

            if (profile.CanonicalCost <= 0)
                bonus += CombatScoreWeights.FreeTransformBonus;
            if (energy >= 1)
                bonus += CombatScoreWeights.EarlyTransformBonus;
        }
        else if (profile.Flags.HasFlag(CardMechanicFlags.TransformsCards)) {
            bonus += CombatTransformSimulator.EstimateDamageGain(hand, card) / 2;
        }

        if (profile.AppliedVulnerable > 0) {
            var existing = CombatPowerReader.GetVulnerable(targetEnemy);
            if (existing <= 0) {
                var followup = CombatCardStats.EstimateFollowupAttackDamage(hand, energy);
                bonus += CombatScoreWeights.VulnerableSetupBase
                    + profile.AppliedVulnerable * CombatScoreWeights.VulnerablePerStack
                    + followup / 3;
            }
            else {
                bonus -= CombatScoreWeights.RedundantDebuffPenalty;
            }
        }

        if (profile.AppliedWeak > 0) {
            var existing = CombatPowerReader.GetWeak(targetEnemy);
            bonus += existing <= 0
                ? CombatScoreWeights.WeakSetupBase + profile.AppliedWeak * CombatScoreWeights.WeakPerStack
                : -CombatScoreWeights.RedundantDebuffPenalty / 2;
        }

        return bonus;
    }

    public static bool IsSetupSkill(CardMechanicProfile profile) =>
        profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)
        || profile.Flags.HasFlag(CardMechanicFlags.TransformsCards)
        || profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable)
        || profile.Flags.HasFlag(CardMechanicFlags.AppliesWeak);
}

internal static class CombatScoreWeights {
    public const int FreeTransformBonus = 12;
    public const int EarlyTransformBonus = 20;
    public const int UnusableTransformPenalty = -200;
    public const int VulnerableSetupBase = 18;
    public const int VulnerablePerStack = 8;
    public const int WeakSetupBase = 10;
    public const int WeakPerStack = 5;
    public const int RedundantDebuffPenalty = 12;
    public const int NonSetupSkillPenalty = 40;
}
