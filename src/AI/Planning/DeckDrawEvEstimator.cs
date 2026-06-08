using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat;
using KitLib.AI.Combat.Simulation;

namespace KitLib.AI.Planning;

public sealed record TurnOneMetrics(
    int BeamScore,
    int MaxDamage,
    int AffordableBlock,
    int Incoming,
    int BlockGap,
    bool CanLethal,
    int NonDamageThreat);

/// <summary>Monte Carlo turn-1 metrics from deck shuffle samples.</summary>
public static class DeckDrawEvEstimator {
    public const int DefaultSampleCount = 8;

    public static TurnOneMetrics EstimateAverage(
        JsonObject snapshot,
        JsonObject? offeredCard,
        NextFightNode fight,
        int sampleCount = DefaultSampleCount) {
        if (sampleCount <= 0)
            sampleCount = DefaultSampleCount;

        long beamTotal = 0;
        long maxDamageTotal = 0;
        long blockTotal = 0;
        long blockGapTotal = 0;
        long nonDamageTotal = 0;
        int lethalCount = 0;
        int incoming = fight.IncomingTurn1;

        for (int s = 0; s < sampleCount; s++) {
            var state = DeckCombatStateFactory.BuildOpeningTurn(
                snapshot, offeredCard, fight.Enemies, s);
            var metrics = EstimateSingle(state, incoming);
            beamTotal += metrics.BeamScore;
            maxDamageTotal += metrics.MaxDamage;
            blockTotal += metrics.AffordableBlock;
            blockGapTotal += metrics.BlockGap;
            nonDamageTotal += metrics.NonDamageThreat;
            if (metrics.CanLethal)
                lethalCount++;
        }

        int n = sampleCount;
        var result = new TurnOneMetrics(
            (int)(beamTotal / n),
            (int)(maxDamageTotal / n),
            (int)(blockTotal / n),
            incoming,
            (int)(blockGapTotal / n),
            lethalCount >= n / 2,
            (int)(nonDamageTotal / n));
        return result;
    }

    public static TurnOneMetrics EstimateSingle(CombatState state, int incoming) {
        int beamScore = CombatBeamSearch.RunBestScore(state, CombatBeamSearch.ShallowMacro);
        int maxDamage = SimLethalChecker.EstimateMaxDamage(state);
        int affordableBlock = BlockDefensePolicy.AffordableBlockTotal(state);
        int blockGap = Math.Max(0, incoming - affordableBlock - state.PlayerBlock);
        bool canLethal = SimLethalChecker.CanLethal(state, out _);
        int nonDamage = ThreatModel.TotalNonDamageThreat(state);

        if (beamScore == int.MinValue)
            beamScore = FallbackTurnScore(state, incoming, maxDamage, affordableBlock, canLethal);

        return new TurnOneMetrics(
            beamScore,
            maxDamage,
            affordableBlock,
            incoming,
            blockGap,
            canLethal,
            nonDamage);
    }

    static int FallbackTurnScore(
        CombatState state,
        int incoming,
        int maxDamage,
        int affordableBlock,
        bool canLethal) {
        int score = maxDamage * 2 + affordableBlock;
        if (canLethal)
            score += 80;
        score -= Math.Max(0, incoming - affordableBlock - state.PlayerBlock) * 6;
        score -= ThreatModel.TotalNonDamageThreat(state);
        return score;
    }
}
