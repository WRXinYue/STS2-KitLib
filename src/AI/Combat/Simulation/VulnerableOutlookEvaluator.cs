using System;
using System.Linq;

namespace KitLib.AI.Combat.Simulation;

/// <summary>Expected offensive bonus from vulnerable stacks decaying over future player turns.</summary>
public static class VulnerableOutlookEvaluator {
    public const int DefaultHorizonTurns = 10;

    public static int Estimate(CombatState state, int horizonTurns = DefaultHorizonTurns) {
        if (horizonTurns <= 0 || state.AliveEnemyCount == 0)
            return 0;

        if (!state.Enemies.Any(e => e.IsAlive && e.Vulnerable > 0))
            return 0;

        int avgDamage = AverageCyclingAttackDamage(state);
        if (avgDamage <= 0)
            return 0;

        int bonusPerHit = Math.Max(0, (int)Math.Round(avgDamage * 0.5));
        if (bonusPerHit <= 0)
            return 0;

        double attacksPerTurn = EstimateAttackHitsPerTurn(state);
        if (attacksPerTurn <= 0)
            return 0;

        int focusIndex = CombatSetupEvaluator.PrimaryAttackTargetIndex(state);
        int vulnTargets = state.Enemies.Count(e => e.IsAlive && e.Vulnerable > 0);
        int total = 0;

        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive || enemy.Vulnerable <= 0)
                continue;

            float share = TargetShare(enemy.Index, focusIndex, vulnTargets);
            for (int t = 0; t < horizonTurns; t++) {
                if (enemy.Vulnerable - t <= 0)
                    break;

                total += (int)Math.Round(attacksPerTurn * share) * bonusPerHit;
            }
        }

        return total;
    }

    static int AverageCyclingAttackDamage(CombatState state) {
        var cards = state.DrawPile
            .Concat(state.DiscardPile)
            .Where(c => c.Damage > 0
                && (string.Equals(c.CardType, "Attack", StringComparison.OrdinalIgnoreCase) || c.Damage > 0))
            .ToList();

        if (cards.Count == 0)
            return 0;

        long sum = 0;
        foreach (var card in cards)
            sum += CombatDamageCalc.OutgoingDamage(card, state.Modifiers, vulnerableOnTarget: 0);

        return (int)Math.Round((double)sum / cards.Count);
    }

    static double EstimateAttackHitsPerTurn(CombatState state) {
        int energy = state.MaxEnergy;
        int draw = RelicCombatRules.PlannedHandDraw(state);
        if (energy <= 0 || draw <= 0)
            return 0;

        int deck = state.DrawPile.Count + state.DiscardPile.Count;
        int sampleSize = deck > 0 ? Math.Min(deck, Math.Max(draw * 4, 20)) : draw * 4;
        var sample = DrawPlanner.PeekTop(state, sampleSize);
        if (sample.Count == 0)
            return Math.Min(energy, draw);

        int windows = 0;
        int hitTotal = 0;
        for (int start = 0; start + draw <= sample.Count; start += Math.Max(1, draw)) {
            int remaining = energy;
            int hits = 0;
            for (int i = start; i < start + draw; i++) {
                var card = sample[i];
                if (card.Damage <= 0)
                    continue;
                if (!string.Equals(card.CardType, "Attack", StringComparison.OrdinalIgnoreCase))
                    continue;

                int cost = CombatDamageCalc.PlanningCost(card, state.Modifiers, energy);
                if (cost > remaining)
                    continue;

                remaining -= cost;
                hits++;
            }

            hitTotal += hits;
            windows++;
        }

        if (windows <= 0)
            return 0;

        return (double)hitTotal / windows;
    }

    static float TargetShare(int enemyIndex, int focusIndex, int vulnTargetCount) {
        if (vulnTargetCount <= 1)
            return enemyIndex == focusIndex ? 1f : 0f;
        if (enemyIndex == focusIndex)
            return 0.85f;
        return 0.15f / Math.Max(1, vulnTargetCount - 1);
    }
}
