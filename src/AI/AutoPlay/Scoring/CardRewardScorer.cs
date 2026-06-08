using System.Text.Json.Nodes;
using KitLib.AI;
using KitLib.AI.Core.Schema;
using KitLib.AI.Planning;

namespace KitLib.AI.AutoPlay.Scoring;

public static class CardRewardScorer {
    public static GameAction PickBest(JsonObject snapshot) {
        var offered = snapshot["offeredCards"]?.AsArray();
        var deckSize = snapshot["deck"]?.AsArray()?.Count ?? 0;
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);

        if (offered == null || offered.Count == 0) {
            return new GameAction {
                Type = ActionType.PickCardReward,
                TargetIndex = 0,
                Reason = "No offer data — pick first",
            };
        }

        int bestIdx = -1;
        int bestScore = int.MinValue;
        int bestMarginal = 0;
        int bestNextFight = 0;

        for (int i = 0; i < offered.Count; i++) {
            if (offered[i] is not JsonObject card) continue;
            var breakdown = MacroScorerHelper.ScoreCardOfferBreakdown(card, plan, deckSize, snapshot);
            if (breakdown.Total > bestScore) {
                bestScore = breakdown.Total;
                bestMarginal = breakdown.Marginal;
                bestNextFight = breakdown.NextFight;
                bestIdx = card["index"]?.GetValue<int>() ?? i;
            }
        }

        int skipCost = DeckEvaluator.SkipOpportunityCost(metrics, plan, snapshot);
        if (bestIdx < 0 || bestScore < skipCost)
            return SkipAction(metrics, plan, bestScore, bestMarginal, bestNextFight, skipCost);

        var name = FindOfferName(offered, bestIdx);
        LogPick(snapshot, name, bestScore, bestMarginal, bestNextFight, skipCost);
        return new GameAction {
            Type = ActionType.PickCardReward,
            TargetIndex = bestIdx,
            Reason = $"Card pick [{name}] score={bestScore} skipCost={skipCost} marginal={bestMarginal} nextFight={bestNextFight}",
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

    static GameAction SkipAction(
        DeckMetrics metrics,
        DeckPlan plan,
        int bestScore,
        int marginal,
        int nextFight,
        int skipCost) {
        int quality = DeckEvaluator.DeckQualityScore(metrics, plan);
        AiDecisionLog.Record("AutoPlay",
            $"card skip best={bestScore} skipCost={skipCost} marginal={marginal} nextFight={nextFight} quality={quality}");
        return new GameAction {
            Type = ActionType.SkipCardReward,
            Reason = $"Skip (best={bestScore} skipCost={skipCost} marginal={marginal} nextFight={nextFight} quality={quality})",
        };
    }

    static void LogPick(JsonObject snapshot, string name, int score, int marginal, int nextFight, int skipCost) {
        var preview = snapshot["nextFightPreview"]?.AsArray();
        var fightHint = preview != null && preview.Count > 0
            ? preview[0]?["encounterId"]?.GetValue<string>() ?? "?"
            : "none";
        AiDecisionLog.Record("AutoPlay",
            $"card pick [{name}:+{score}] skipCost={skipCost} marginal={marginal} nextFight={nextFight} vs={fightHint}");
    }
}
