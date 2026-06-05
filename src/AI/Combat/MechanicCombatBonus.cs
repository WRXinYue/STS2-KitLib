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
        int energy,
        bool suppressTransform = false) {
        var bonus = 0;

        if (profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)) {
            var attacks = CombatTransformSimulator.CountTransformableAttacks(hand);
            if (attacks == 0)
                return CombatScoreWeights.UnusableTransformPenalty;

            var skillCost = card["cost"]?.GetValue<int>() ?? 0;
            if (skillCost > energy)
                return CombatScoreWeights.UnusableTransformPenalty;

            var turnDelta = CombatTransformSimulator.EstimateTurnDamageDelta(hand, card, energy);
            if (turnDelta <= 0)
                return suppressTransform
                    ? -CombatScoreWeights.TransformThreatDiscountMax
                    : CombatScoreWeights.NegativeTransformPenalty;

            bonus += turnDelta;

            if (!suppressTransform) {
                if (profile.CanonicalCost <= 0)
                    bonus += CombatScoreWeights.FreeTransformBonus;
                if (attacks >= 2 && energy >= 2)
                    bonus += Math.Min(CombatScoreWeights.TurnOpenTransformBonus, turnDelta);
            }
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

internal static class CombatEvalWeights {
    public const int MidTurnNetMultiplier = 6;
    public const int MidTurnLowHpNetBonus = 2;
    public const int TerminalHpMultiplier = 8;
    public const int TerminalNetPenalty = 12;
    public const int TerminalFullBlockBonus = 80;
    public const int TerminalBlockRewardPerPoint = 3;
    public const int BlockCoverPerPoint = 4;
    public const int LowHpNextTurnPenalty = 2;
    public const int UnusedEnergyExposedNetPenalty = 6;
    public const int UnsafeAttackPenaltyPerNet = 8;
}

internal static class CombatScoreWeights {
    public const int FreeTransformBonus = 12;
    public const int TurnOpenTransformBonus = 40;
    public const int UnusableTransformPenalty = -200;
    public const int NegativeTransformPenalty = -80;
    public const int AttackBeforeTransformCap = 50;
    public const int TransformThreatDiscountMax = 20;
    public const int VulnerableSetupBase = 18;
    public const int VulnerablePerStack = 8;
    public const int WeakSetupBase = 10;
    public const int WeakPerStack = 5;
    public const int RedundantDebuffPenalty = 12;
    public const int NonSetupSkillPenalty = 40;
}
