using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace KitLib.AI.Planning;

/// <summary>Scores card offers by simulating upcoming combats on the planned route.</summary>
public static class NextFightDeckEvaluator {
    static readonly object Gate = new();
    static int _cacheKey;
    static int _baselineScore;

    public static int ScoreOfferDelta(JsonObject snapshot, JsonObject offeredCard, DeckPlan plan) {
        var route = NextFightRoute.ResolveFromSnapshot(snapshot);
        if (route.Count == 0)
            return 0;

        int baseline = GetBaselineScore(snapshot, plan, route);
        int withOffer = EvaluateDeck(snapshot, offeredCard, route);
        return Math.Clamp(withOffer - baseline, -80, 80);
    }

    public static int GetBaselineRouteScore(JsonObject snapshot, DeckPlan plan) {
        var route = NextFightRoute.ResolveFromSnapshot(snapshot);
        if (route.Count == 0)
            return 0;
        return GetBaselineScore(snapshot, plan, route);
    }

    static int GetBaselineScore(JsonObject snapshot, DeckPlan plan, IReadOnlyList<NextFightNode> route) {
        int key = ComputeCacheKey(snapshot);
        lock (Gate) {
            if (key == _cacheKey)
                return _baselineScore;

            _cacheKey = key;
            _baselineScore = EvaluateDeck(snapshot, null, route);
            return _baselineScore;
        }
    }

    static int EvaluateDeck(
        JsonObject snapshot,
        JsonObject? offeredCard,
        IReadOnlyList<NextFightNode> route) {
        float total = 0f;
        float weightSum = 0f;

        foreach (var fight in route) {
            var metrics = DeckDrawEvEstimator.EstimateAverage(snapshot, offeredCard, fight);
            total += fight.Weight * ScoreMetrics(metrics, snapshot);
            weightSum += fight.Weight;
        }

        if (weightSum <= 0f)
            return 0;

        return (int)Math.Round(total / weightSum);
    }

    static int ScoreMetrics(TurnOneMetrics metrics, JsonObject snapshot) {
        int score = Math.Clamp(metrics.BeamScore / 8, -120, 120);

        if (metrics.CanLethal)
            score += 25;

        score += Math.Clamp(metrics.MaxDamage / 4, 0, 40);
        score += Math.Clamp(metrics.AffordableBlock / 3, 0, 30);

        if (metrics.BlockGap > 0) {
            score -= Math.Min(metrics.BlockGap * 8, 80);
            var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
            if (metrics.BlockGap >= hp)
                score -= 60;
        }

        score -= Math.Min(metrics.NonDamageThreat * 2, 40);
        return Math.Clamp(score, -120, 120);
    }

    static int ComputeCacheKey(JsonObject snapshot) {
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        var deckSize = snapshot["deck"]?.AsArray()?.Count ?? 0;
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        return HashCode.Combine(floor, deckSize, hp);
    }

    public static void ClearCache() {
        lock (Gate) {
            _cacheKey = 0;
            _baselineScore = 0;
        }
    }
}
