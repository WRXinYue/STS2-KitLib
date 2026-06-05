using System.Collections.Generic;
using System.Linq;

namespace DevMode.AI.Combat.Simulation;

public static class LegalActionGenerator {
    public static IEnumerable<SimCombatAction> Generate(CombatState state) {
        bool anyPlay = false;

        foreach (var action in GenerateRaw(state)) {
            if (action.Kind == SimActionKind.PlayCard)
                anyPlay = true;
            if (!CombatActionHeuristic.ShouldPrune(state, action))
                yield return action;
        }

        if (anyPlay || state.Energy >= 0)
            yield return new SimCombatAction(SimActionKind.EndTurn);
    }

    public static IEnumerable<SimCombatAction> GenerateOrdered(CombatState state, int maxActions = int.MaxValue) =>
        Generate(state)
            .OrderByDescending(a => CombatActionHeuristic.QuickScore(state, a))
            .Take(maxActions);

    static IEnumerable<SimCombatAction> GenerateRaw(CombatState state) {
        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!card.CanPlay || card.Cost > state.Energy) continue;

            if (card.IsAoe || card.TargetType is "AllEnemy") {
                yield return new SimCombatAction(SimActionKind.PlayCard, i, -1);
                continue;
            }

            if (card.TargetType is "AnyEnemy" or "AllEnemy" or "" && card.IsAttack) {
                foreach (var enemyIndex in OrderedAttackTargets(state))
                    yield return new SimCombatAction(SimActionKind.PlayCard, i, enemyIndex);
                continue;
            }

            yield return new SimCombatAction(SimActionKind.PlayCard, i, -1);
        }
    }

    static IEnumerable<int> OrderedAttackTargets(CombatState state) {
        return state.Enemies
            .Where(e => ThreatModel.IsViableAttackTarget(state, e))
            .OrderByDescending(e => e.IsMinion ? 0 : 1)
            .ThenBy(e => e.EffectiveHp)
            .ThenByDescending(e => e.IntentDamage)
            .ThenByDescending(e => ThreatModel.NextTurnAttackOn(e))
            .Take(4)
            .Select(e => e.Index);
    }
}
