using System;
using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;
using KitLib.AI.Planning;

namespace KitLib.AI.AutoPlay.Scoring;

public static class RelicScorer {
    public static GameAction PickBest(JsonObject snapshot) {
        var offered = snapshot["offeredRelics"]?.AsArray();
        var owned = snapshot["relics"]?.AsArray();
        var plan = DeckPlanInferer.Infer(snapshot);

        if (offered == null || offered.Count == 0) {
            return new GameAction {
                Type = ActionType.PickRelic,
                TargetIndex = 0,
                Reason = "First relic (no offer data)",
            };
        }

        int bestIdx = 0;
        int bestScore = int.MinValue;

        for (int i = 0; i < offered.Count; i++) {
            if (offered[i] is not JsonObject relic)
                continue;

            int score = MacroScorerHelper.ScoreRelicOffer(relic, plan, owned, snapshot);
            if (score > bestScore) {
                bestScore = score;
                bestIdx = relic["index"]?.GetValue<int>() ?? i;
            }
        }

        var name = offered[bestIdx]?["name"]?.GetValue<string>() ?? $"relic {bestIdx}";
        return new GameAction {
            Type = ActionType.PickRelic,
            TargetIndex = bestIdx,
            Reason = $"Relic pick [{name}] score={bestScore}",
        };
    }
}
