using System;
using System.Linq;
using DevMode.AI.Combat;

namespace DevMode.AI.Combat.Simulation;

public static class CombatEvaluator {
    /// <summary>Mid-turn board value while cards remain to play.</summary>
    public static int Evaluate(CombatState state) => EvaluateMidTurn(state);

    public static int EvaluateMidTurn(CombatState state) {
        var incoming = ThreatModel.IncomingDamage(state);
        var net = ThreatModel.NetDamageAfterBlock(state);
        var netMultiplier = incoming > 0 ? 4 : 3;

        int score = state.PlayerHp * 2;
        score -= net * netMultiplier;
        score -= state.StatusDamage * 2;
        score -= ThreatEconomy.TotalPressure(state) / 4;
        score += DeckPollutionEvaluator.ExpectedPlayableDamage(state) / 2;
        score += PileRhythmEvaluator.RemainingTurnPotential(state) / 4;

        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive) continue;
            score -= enemy.CurrentHp * 2;
            score -= enemy.IntentDamage * 3;
            if (incoming == 0)
                score -= enemy.NonDamageThreat / 4;
            if (enemy.Vulnerable > 0)
                score += Math.Min(16, enemy.Vulnerable * 5);
        }

        score -= state.AliveEnemyCount * 6;
        score -= state.Energy * 2;

        if (state.AliveEnemyCount == 0)
            score += 300;

        score -= CombatSetupEvaluator.ComputeSetupDebt(state);

        return score;
    }

    /// <summary>Player ended turn — apply incoming damage and score survival.</summary>
    public static int EvaluateTerminal(CombatState state) {
        if (state.AliveEnemyCount == 0)
            return 600 + state.PlayerHp;

        var incoming = ThreatModel.IncomingDamage(state);
        var net = ThreatModel.NetDamageAfterBlock(state);
        var hpAfter = Math.Max(0, state.PlayerHp - net - state.StatusDamage);

        int score = hpAfter * 5;
        if (hpAfter <= 0)
            return -1200;

        if (incoming > 0 && net <= 0 && state.PlayerBlock > 0)
            score += Math.Min(state.PlayerBlock, incoming) * 2;

        foreach (var enemy in state.Enemies.Where(e => e.IsAlive)) {
            score -= enemy.CurrentHp * 2;
            score -= enemy.IntentDamage * 4;
        }

        score -= state.AliveEnemyCount * 10;
        score -= ThreatModel.NextTurnIncoming(state);
        score -= DeckPollutionEvaluator.ImmediatePollutionCost(state) / 2;
        score += DeckPollutionEvaluator.ExpectedPlayableDamage(state);
        score += DeckPollutionEvaluator.ExpectedPlayableBlock(state) / 2;
        score += PileRhythmEvaluator.DrawPileOutlook(state) / 3;
        score -= CombatSetupEvaluator.ComputeSetupDebt(state);

        return score;
    }
}
