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
        var netMultiplier = CombatEvalWeights.MidTurnNetMultiplier;
        if (net > 0 && state.PlayerMaxHp > 0
            && (float)state.PlayerHp / state.PlayerMaxHp < 0.5f)
            netMultiplier += CombatEvalWeights.MidTurnLowHpNetBonus;

        int score = state.PlayerHp * 2;
        score -= net * netMultiplier;
        score -= state.StatusDamage * 2;
        score -= ThreatEconomy.TotalPressure(state) / 4;

        var playDamage = DeckPollutionEvaluator.ExpectedPlayableDamage(state);
        score += net > 0 ? playDamage / 4 : playDamage / 2;

        score += PileRhythmEvaluator.RemainingTurnPotential(state) / 4;
        score += BlockDefensePolicy.FullBlockValue(state);

        if (net <= 0 && incoming > 0 && state.PlayerBlock > 0)
            score += Math.Min(state.PlayerBlock, incoming) * CombatEvalWeights.TerminalBlockRewardPerPoint;

        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive) continue;
            score -= enemy.CurrentHp * 2;
            score -= enemy.IntentDamage * 3;
            if (incoming == 0)
                score -= enemy.NonDamageThreat / 4;
            if (enemy.Vulnerable > 0) {
                var expected = CombatSetupEvaluator.EstimateGreedyAttackDamageOn(state, enemy.Index);
                score += Math.Min(20, enemy.Vulnerable * 5 + expected / 4);
            }
        }

        score -= state.AliveEnemyCount * 6;
        score -= state.Energy * 2;

        if (state.AliveEnemyCount == 0)
            score += 300;

        score -= CombatSetupEvaluator.ComputeSetupDebt(state);
        score -= CombatSetupEvaluator.ComputeWastedVulnerablePenalty(state);

        return score;
    }

    /// <summary>Player ended turn — apply incoming damage and score survival.</summary>
    public static int EvaluateTerminal(CombatState state) {
        if (state.AliveEnemyCount == 0)
            return 600 + state.PlayerHp;

        var incoming = ThreatModel.IncomingDamage(state);
        var net = ThreatModel.NetDamageAfterBlock(state);
        var hpAfter = Math.Max(0, state.PlayerHp - net - state.StatusDamage);

        int score = hpAfter * CombatEvalWeights.TerminalHpMultiplier;
        if (hpAfter <= 0)
            return -1200;

        if (net > 0)
            score -= net * CombatEvalWeights.TerminalNetPenalty;

        if (incoming > 0 && net <= 0 && state.PlayerBlock > 0) {
            score += CombatEvalWeights.TerminalFullBlockBonus;
            score += Math.Min(state.PlayerBlock, incoming) * CombatEvalWeights.TerminalBlockRewardPerPoint;
        }

        foreach (var enemy in state.Enemies.Where(e => e.IsAlive)) {
            score -= enemy.CurrentHp * 2;
            score -= enemy.IntentDamage * 4;
        }

        score -= state.AliveEnemyCount * 10;
        score -= ThreatModel.NextTurnIncoming(state);

        var hpRatio = state.PlayerMaxHp > 0 ? (float)hpAfter / state.PlayerMaxHp : 1f;
        if (hpRatio < 0.45f && net > 0)
            score -= net * 8;

        if (hpRatio < 0.5f || net > 0)
            score += DeckPollutionEvaluator.ExpectedPlayableDamage(state) / 2;
        else
            score += DeckPollutionEvaluator.ExpectedPlayableDamage(state);

        var nextIncoming = ThreatModel.NextTurnIncoming(state);
        var blockWeight = nextIncoming >= 8 ? 3 : 2;
        score += DeckPollutionEvaluator.ExpectedPlayableBlock(state) * blockWeight / 4;

        score += PileRhythmEvaluator.DrawPileOutlook(state) / 3;
        score -= DeckPollutionEvaluator.ImmediatePollutionCost(state) / 2;
        score -= CombatSetupEvaluator.ComputeSetupDebt(state);
        score -= CombatSetupEvaluator.ComputeWastedVulnerablePenalty(state);

        return score;
    }
}
