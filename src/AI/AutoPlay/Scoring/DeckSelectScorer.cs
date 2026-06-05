using System;
using System.Text.Json.Nodes;
using DevMode.AI.Core.Schema;
using DevMode.AI.Knowledge;
using DevMode.AI.Planning;

namespace DevMode.AI.AutoPlay.Scoring;

/// <summary>Picks cards on <see cref="NDeckCardSelectScreen"/> (smith upgrade, shop removal, events).</summary>
public static class DeckSelectScorer {
    public static GameAction PickBest(JsonObject snapshot) {
        var context = snapshot["deckSelectContext"]?.GetValue<string>() ?? "";
        return context switch {
            "upgrade" => PickUpgrade(snapshot),
            "remove" => PickRemove(snapshot),
            _ => CardRewardScorer.PickBest(snapshot),
        };
    }

    public static GameAction PickUpgrade(JsonObject snapshot) {
        var plan = DeckPlanInferer.Infer(snapshot);
        var composition = AnalyzeComposition(snapshot);
        var idx = FindBestDeckCardIndex(snapshot, plan, composition, forUpgrade: true);
        var name = FindOfferName(snapshot, idx);
        return new GameAction {
            Type = ActionType.PickCardReward,
            TargetIndex = idx,
            Reason = $"Upgrade [{name}]",
        };
    }

    public static GameAction PickRemove(JsonObject snapshot) {
        var plan = DeckPlanInferer.Infer(snapshot);
        var composition = AnalyzeComposition(snapshot);
        var idx = FindBestDeckCardIndex(snapshot, plan, composition, forUpgrade: false);
        var name = FindOfferName(snapshot, idx);
        return new GameAction {
            Type = ActionType.PickCardReward,
            TargetIndex = idx,
            Reason = $"Remove [{name}]",
        };
    }

    static DeckComposition AnalyzeComposition(JsonObject snapshot) {
        var deck = snapshot["deck"]?.AsArray();
        return deck != null
            ? DeckCardScoring.AnalyzeComposition(deck)
            : new DeckComposition(0, 0, 0);
    }

    static int FindBestDeckCardIndex(
        JsonObject snapshot,
        DeckPlan plan,
        DeckComposition composition,
        bool forUpgrade) {
        var offered = snapshot["offeredCards"]?.AsArray();
        if (offered == null || offered.Count == 0) return 0;

        int bestIdx = 0;
        int bestScore = forUpgrade ? int.MinValue : int.MaxValue;

        for (int i = 0; i < offered.Count; i++) {
            if (offered[i] is not JsonObject card) continue;
            int idx = card["index"]?.GetValue<int>() ?? i;

            if (forUpgrade) {
                var upgrade = card["upgradeLevel"]?.GetValue<int>() ?? 0;
                var maxUpgrade = card["maxUpgradeLevel"]?.GetValue<int>() ?? 1;
                if (upgrade >= maxUpgrade) continue;

                int score = DeckCardScoring.ScoreInDeck(card, plan, composition);
                if (score > bestScore) {
                    bestScore = score;
                    bestIdx = idx;
                }
            }
            else {
                int score = DeckCardScoring.ScoreInDeck(card, plan, composition);
                score -= CodexPriorCatalog.GetRemoveBonus(
                    snapshot["characterId"]?.GetValue<string>(),
                    card["id"]?.GetValue<string>());
                if (score < bestScore) {
                    bestScore = score;
                    bestIdx = idx;
                }
            }
        }

        return bestIdx;
    }

    static string FindOfferName(JsonObject snapshot, int targetIdx) {
        var offered = snapshot["offeredCards"]?.AsArray();
        if (offered == null) return $"card {targetIdx}";

        for (int i = 0; i < offered.Count; i++) {
            if (offered[i] is not JsonObject card) continue;
            var idx = card["index"]?.GetValue<int>() ?? i;
            if (idx == targetIdx)
                return card["name"]?.GetValue<string>() ?? $"card {targetIdx}";
        }
        return $"card {targetIdx}";
    }
}
