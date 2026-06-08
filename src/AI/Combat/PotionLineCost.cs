using System.Linq;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

/// <summary>Opportunity cost of potions consumed on a beam line (retain value + situational waste).</summary>
internal static class PotionLineCost {
    public static int Estimate(CombatState decisionRoot, CombatState lineStart) {
        if (!lineStart.PotionUsedThisTurn)
            return 0;

        int cost = 0;
        foreach (var consumed in decisionRoot.Potions) {
            if (lineStart.Potions.Any(p => p.Slot == consumed.Slot))
                continue;

            cost += PotionTierCatalog.GetRetainScore(consumed.Id);
            cost += SituationalWastePenalty(lineStart, consumed.Id);
        }

        return cost;
    }

    static int SituationalWastePenalty(CombatState lineStart, string potionId) {
        if (!PotionCombatEffectData.TryGetProfile(potionId, out var profile))
            return 0;

        if (profile.Effects.Any(e => e.Kind == PotionCombatEffectKind.GainEnergy)
            && !CombatCardCost.HasAffordablePlay(lineStart))
            return 60;

        if (!profile.Effects.Any(e =>
                e.Kind == PotionCombatEffectKind.ApplyWeak
                || e.Kind == PotionCombatEffectKind.ApplyVulnerable))
            return 0;

        int attackPressure = ThreatModel.ScheduledAttackPressure(lineStart);
        int nonDamage = ThreatModel.TotalNonDamageThreat(lineStart);
        int penalty = 0;

        if (PotionUseScoring.IsAttackDebuffLowValue(lineStart, profile))
            penalty += 40;

        if (nonDamage > attackPressure)
            penalty += 25;

        return penalty;
    }
}
