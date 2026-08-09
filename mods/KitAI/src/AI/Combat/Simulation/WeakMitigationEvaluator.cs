using System;
using System.Linq;

namespace KitLib.AI.Combat.Simulation;

/// <summary>Expected incoming damage prevented by enemy weak stacks over upcoming intent steps.</summary>
public static class WeakMitigationEvaluator {
    public const int DefaultHorizonTurns = 3;

    public static int Estimate(CombatState state, int horizonTurns = DefaultHorizonTurns) {
        if (horizonTurns <= 0 || state.AliveEnemyCount == 0)
            return 0;

        int total = 0;
        foreach (var enemy in state.Enemies.Where(e => e.IsAlive && e.Weak > 0)) {
            for (int step = 0; step < horizonTurns && step < enemy.IntentSteps.Length; step++) {
                int stacks = Math.Max(0, enemy.Weak - step);
                if (stacks <= 0)
                    break;

                int raw = ThreatModel.RawIntentDamageAtStep(enemy, step);
                total += DebuffDamageCalc.WeakIncomingSaved(raw, stacks);
            }
        }

        return total;
    }
}
