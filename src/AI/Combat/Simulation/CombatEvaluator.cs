using System;
using System.Linq;

namespace DevMode.AI.Combat.Simulation;

public static class CombatEvaluator {
    public static int Evaluate(CombatState state) {
        var incoming = ThreatModel.IncomingDamage(state);
        var net = ThreatModel.NetDamageAfterBlock(state);
        var nextIncoming = ThreatModel.NextTurnIncoming(state);
        var netMultiplier = incoming > 0 ? 4 : 3;

        int score = state.PlayerHp;
        score -= net * netMultiplier;
        score -= state.StatusDamage * 2;
        score -= nextIncoming / 2;

        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive) continue;
            score -= enemy.CurrentHp;
            if (enemy.Vulnerable > 0)
                score += Math.Min(12, enemy.Vulnerable * 4);
        }

        score -= state.AliveEnemyCount * 5;
        score -= state.Energy * 2;

        if (state.AliveEnemyCount == 0)
            score += 200;

        return score;
    }
}
