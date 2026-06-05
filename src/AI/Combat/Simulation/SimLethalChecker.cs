using System.Linq;

namespace DevMode.AI.Combat.Simulation;

public static class SimLethalChecker {
    public static bool CanLethal(CombatState state, out int targetIndex) {
        targetIndex = -1;

        foreach (var enemy in state.Enemies.OrderByDescending(e => !e.IsMinion).ThenBy(e => e.CurrentHp)) {
            if (!enemy.IsAlive) continue;
            if (EstimateMaxDamage(state) < enemy.EffectiveHp) continue;
            targetIndex = enemy.Index;
            return true;
        }

        return false;
    }

    public static bool CanLethalAfterTransform(CombatState state, out int targetIndex, out int transformHandIndex) {
        targetIndex = -1;
        transformHandIndex = -1;

        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!CombatTransformSimulator.IsHandAttackTransform(card.Profile)) continue;
            if (!card.CanPlay || card.Cost > state.Energy) continue;

            var projected = CombatSimulator.Apply(state, new SimCombatAction(SimActionKind.PlayCard, i, -1));
            if (!CanLethal(projected, out targetIndex)) continue;
            transformHandIndex = i;
            return true;
        }

        return false;
    }

    public static int EstimateMaxDamage(CombatState state) {
        int total = 0;
        int energy = state.Energy;

        foreach (var card in state.Hand.OrderByDescending(c => c.Damage)) {
            if (!card.CanPlay || !card.IsAttack || card.Cost > energy) continue;
            if (card.IsAoe && AoeDamageEstimator.EstimateAoeKills(state, card.Damage) > 0)
                return int.MaxValue / 4;
            total += card.Damage;
            energy -= card.Cost;
        }

        return total;
    }
}
