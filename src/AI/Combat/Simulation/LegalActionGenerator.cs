using System.Collections.Generic;

namespace DevMode.AI.Combat.Simulation;

public static class LegalActionGenerator {
    public static IEnumerable<SimCombatAction> Generate(CombatState state) {
        bool anyPlay = false;

        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!card.CanPlay || card.Cost > state.Energy) continue;

            anyPlay = true;
            if (card.IsAoe || card.TargetType is "AllEnemy") {
                yield return new SimCombatAction(SimActionKind.PlayCard, i, -1);
                continue;
            }

            if (card.TargetType is "AnyEnemy" or "AllEnemy" or "" && card.IsAttack) {
                foreach (var enemy in state.Enemies) {
                    if (!enemy.IsAlive) continue;
                    yield return new SimCombatAction(SimActionKind.PlayCard, i, enemy.Index);
                }
                continue;
            }

            yield return new SimCombatAction(SimActionKind.PlayCard, i, -1);
        }

        if (anyPlay || state.Energy >= 0)
            yield return new SimCombatAction(SimActionKind.EndTurn);
    }
}
