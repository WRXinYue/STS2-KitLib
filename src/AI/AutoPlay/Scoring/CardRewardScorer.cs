using System;
using System.Text.Json.Nodes;
using DevMode.AI.Core.Schema;
using DevMode.AI.Knowledge;
using DevMode.AI.Planning;

namespace DevMode.AI.AutoPlay.Scoring;

public static class CardRewardScorer {
    const int MinPickScore = 9;

    public static GameAction PickBest(JsonObject snapshot) {
        var offered = snapshot["offeredCards"]?.AsArray();
        var deckSize = snapshot["deck"]?.AsArray()?.Count ?? 0;
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);

        if (offered == null || offered.Count == 0) {
            if (ShouldSkip(metrics, plan, snapshot))
                return Skip("No offers — skip (lean deck)");
            return new GameAction {
                Type = ActionType.PickCardReward,
                TargetIndex = 0,
                Reason = "No offer data — pick first",
            };
        }

        int bestIdx = -1;
        int bestScore = int.MinValue;
        int skipScore = SkipScore(metrics, plan, snapshot);

        for (int i = 0; i < offered.Count; i++) {
            if (offered[i] is not JsonObject card) continue;
            int score = MacroScorerHelper.ScoreCardOffer(card, plan, deckSize, snapshot);
            if (score > bestScore) {
                bestScore = score;
                bestIdx = card["index"]?.GetValue<int>() ?? i;
            }
        }

        if (bestIdx < 0 || bestScore < MinPickScore || bestScore < skipScore)
            return Skip($"Skip (best {bestScore} < threshold {Math.Max(MinPickScore, skipScore)})");

        var name = FindOfferName(offered, bestIdx);
        return new GameAction {
            Type = ActionType.PickCardReward,
            TargetIndex = bestIdx,
            Reason = $"Card pick [{name}] score={bestScore}",
        };
    }

    static string FindOfferName(JsonArray offered, int targetIdx) {
        for (int i = 0; i < offered.Count; i++) {
            if (offered[i] is not JsonObject card) continue;
            var idx = card["index"]?.GetValue<int>() ?? i;
            if (idx == targetIdx)
                return card["name"]?.GetValue<string>() ?? $"card {targetIdx}";
        }
        return $"card {targetIdx}";
    }

    static bool ShouldSkip(DeckMetrics metrics, DeckPlan plan, JsonObject snapshot) =>
        SkipScore(metrics, plan, snapshot) > 0;

    static int SkipScore(DeckMetrics metrics, DeckPlan plan, JsonObject snapshot) {
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        var actIndex = snapshot["actIndex"]?.GetValue<int>() ?? 0;

        var score = (int)Math.Round(DeckPlanInferer.DilutionPenalty(metrics.DeckSize, plan));
        score += (int)Math.Round(plan.ThinPreference * 14f);

        if (metrics.StarterBloat <= 0 && metrics.MeanValue >= 12f)
            score += 10;
        if (metrics.RemovalUplift < DeckEvaluator.MinRemovalUplift && metrics.MeanValue >= 10f)
            score += 8;

        if (actIndex >= 2 && metrics.DeckSize > plan.TargetDeckSize + 2)
            score += 15;
        if (floor > 30 && metrics.DeckSize > 20)
            score += 10;
        if (metrics.DeckSize > plan.TargetDeckSize + 5)
            score += 12;

        if (metrics.StarterBloat >= 3)
            score -= 8;

        score += CodexPriorCatalog.GetSkipThresholdOffset(snapshot);

        return score;
    }

    static GameAction Skip(string reason) => new() {
        Type = ActionType.SkipCardReward,
        Reason = reason,
    };
}
