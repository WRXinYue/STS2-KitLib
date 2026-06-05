using System.Linq;
using DevMode.AI.Combat;

namespace DevMode.AI.Combat.Simulation;

public static class SimLethalChecker {
    public static bool CanLethal(CombatState state, out int targetIndex) {
        targetIndex = -1;

        foreach (var enemy in state.Enemies
                     .OrderByDescending(e => !e.IsMinion)
                     .ThenBy(e => e.CurrentHp)) {
            if (!enemy.IsAlive) continue;
            if (LethalExclusions.ShouldSkip(enemy)) continue;
            if (LethalDamageSolver.MaxSingleTargetDamage(state, enemy.Index) < enemy.EffectiveHp) continue;
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
            if (!CombatCardCost.CanAfford(card, state)) continue;

            var projected = CombatSimulator.Apply(state, new SimCombatAction(SimActionKind.PlayCard, i, -1));
            if (!CanLethal(projected, out targetIndex)) continue;
            transformHandIndex = i;
            return true;
        }

        return false;
    }

    public static int EstimateMaxDamage(CombatState state) {
        int best = 0;
        foreach (var enemy in state.Enemies.Where(e => e.IsAlive)) {
            if (LethalExclusions.ShouldSkip(enemy)) continue;
            best = System.Math.Max(best, LethalDamageSolver.MaxSingleTargetDamage(state, enemy.Index));
        }

        return best;
    }
}
