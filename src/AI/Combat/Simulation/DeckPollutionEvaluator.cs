using System;
using System.Linq;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

public static class DeckPollutionEvaluator {
    public static int JunkCount(CombatState state) =>
        state.Hand.Count(c => CombatJunkCard.IsJunkId(c.Id))
        + state.DrawPile.Count(c => c.IsStatus)
        + state.DiscardPile.Count(c => c.IsStatus);

    public static int ImmediatePollutionCost(CombatState state) =>
        JunkCount(state) * CombatJunkCard.DefaultJunkValue;

    public static int ProjectedPollutionCost(CombatState state, int horizonTurns = 2) {
        double total = ImmediatePollutionCost(state);

        foreach (var enemy in state.Enemies.Where(e => e.IsAlive)) {
            for (int i = 0; i < Math.Min(horizonTurns, enemy.IntentSteps.Length); i++) {
                var step = enemy.IntentSteps[i];
                var effects = MoveEffectIndex.GetEffects(enemy.MonsterId, step.MoveId);
                foreach (var effect in effects) {
                    if (effect.Kind != MonsterMoveEffectKind.StatusInject
                        || effect.IsNonDeterministic
                        || effect.Count <= 0)
                        continue;

                    total += effect.Count * CombatJunkCard.DefaultJunkValue * DrawProbability(state);
                }
            }
        }

        return (int)Math.Round(total);
    }

    public static int ExpectedPlayableDamage(CombatState state) {
        int total = 0;
        foreach (var card in state.Hand) {
            if (!card.IsAttack || card.Damage <= 0) continue;
            if (!CombatCardCost.CanAfford(card, state)) continue;

            var damage = card.Damage;
            foreach (var mod in state.Modifiers)
                damage = (int)Math.Round(damage * mod.AttackDamageMultiplier);
            total += Math.Max(0, damage);
        }

        return total;
    }

    public static int ExpectedPlayableBlock(CombatState state) {
        int total = 0;
        foreach (var card in state.Hand) {
            if (!card.IsSkill || card.Block <= 0) continue;

            if (CombatCardCost.EffectiveCost(card, state.Modifiers) > state.Energy) continue;
            total += CombatCardCost.EffectiveBlock(card.Block, state.Modifiers);
        }

        return total;
    }

    static double DrawProbability(CombatState state) {
        int deck = state.DrawPile.Count + state.DiscardPile.Count + state.Hand.Count;
        if (deck <= 0) return 0.5;
        return Math.Min(1.0, CombatPileSimulator.BaseHandDrawCount / (double)Math.Max(1, deck));
    }
}
