using System;
using System.Text.Json.Nodes;
using DevMode.AI.Core.Schema;
using DevMode.AI.Knowledge;
using DevMode.AI.Planning;

namespace DevMode.AI.AutoPlay.Scoring;

public static class CardRewardScorer {
    const int MinPickScore = 6;

    public static GameAction PickBest(JsonObject snapshot) {
        var offered = snapshot["offeredCards"]?.AsArray();
        var deckSize = snapshot["deck"]?.AsArray()?.Count ?? 0;
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);

        if (offered == null || offered.Count == 0) {
            if (ShouldSkip(metrics, plan, snapshot))
                return SkipAction(metrics, plan, 0, SkipScore(metrics, plan, snapshot));
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

        int threshold = Math.Max(MinPickScore, skipScore);
        if (bestIdx < 0 || bestScore < threshold)
            return SkipAction(metrics, plan, bestScore, threshold);

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
        int score = DeckEvaluator.SkipOpportunityCost(metrics, plan, snapshot);
        score += CodexPriorCatalog.GetSkipThresholdOffset(snapshot);
        return score;
    }

    static GameAction SkipAction(DeckMetrics metrics, DeckPlan plan, int bestScore, int threshold) {
        int quality = DeckEvaluator.DeckQualityScore(metrics, plan);
        return new GameAction {
            Type = ActionType.SkipCardReward,
            Reason = $"Skip (best={bestScore} need={threshold} quality={quality} thin={metrics.ThinGap} survival={metrics.SurvivalGap})",
        };
    }
}
