using System;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;
using DevMode.AI.Planning;

namespace DevMode.AI.AutoPlay.Scoring;

internal static class MacroScorerHelper {
    public static int RarityScore(string? rarity) => DeckCardScoring.RarityScore(rarity);

    public static int ScoreCardOffer(JsonObject card, DeckPlan plan, int deckSize, JsonObject? snapshot = null) {
        if (snapshot == null)
            return ScoreCardOfferAbsolute(card, plan, deckSize);

        var metrics = DeckEvaluator.Evaluate(snapshot, plan);
        var deck = snapshot["deck"]?.AsArray();

        int score = DeckEvaluator.MarginalPickScore(snapshot, plan, card);
        score += DeckSynergyEvaluator.ScoreCard(card, plan, snapshot);
        score += DeckSynergyEvaluator.ScoreDeckDilutionOffer(card, plan, metrics, deck);
        score += EarlyCardRewardAdjustments.Score(card, snapshot);
        score += ScaledCodexBonus(card, snapshot, metrics);
        return score;
    }

    static int ScoreCardOfferAbsolute(JsonObject card, DeckPlan plan, int deckSize) {
        var composition = new DeckComposition(0, 0, 0, 0);
        var score = DeckCardScoring.ScoreInDeck(card, plan, composition);
        score -= (int)Math.Round(DeckPlanInferer.DilutionPenalty(deckSize + 1, plan));
        return score;
    }

    static int ScaledCodexBonus(JsonObject card, JsonObject snapshot, DeckMetrics metrics) {
        var characterId = snapshot["characterId"]?.GetValue<string>();
        var cardId = card["id"]?.GetValue<string>();
        var context = snapshot["shopOffers"] != null ? "shop" : "combat_reward";
        var raw = CodexPriorCatalog.GetCardBonus(characterId, cardId, context);
        if (raw == 0) return 0;

        float fit = 1f;
        if (metrics.BlockDeficit == 0 && metrics.DrawDeficit == 0)
            fit *= 0.55f;
        if (metrics.ThinGap > 0)
            fit *= Math.Max(0.25f, 1f - metrics.ThinGap * 0.12f);
        if (metrics.MeanValue >= 12f)
            fit *= 0.65f;
        if (DeckEvaluator.HasTransformCore(snapshot["deck"]?.AsArray())
            && !OffersTransformCore(card))
            fit *= 0.35f;

        return (int)Math.Round(raw * fit);
    }

    static bool OffersTransformCore(JsonObject card) {
        var profile = CardMechanicIndex.InferFromSnapshot(card);
        return profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)
            || profile.Flags.HasFlag(CardMechanicFlags.TransformsCards);
    }

    public static int ScoreRelicOffer(JsonObject relic, DeckPlan plan, JsonArray? ownedRelics, JsonObject? snapshot = null) {
        var id = relic["id"]?.GetValue<string>() ?? "";
        var name = relic["name"]?.GetValue<string>() ?? "";

        if (IsOwned(id, name, ownedRelics))
            return -100;

        var score = RarityScore(relic["rarity"]?.GetValue<string>());
        score += DeckSynergyEvaluator.RelicTagPlanScore(id, plan);
        score += DeckSynergyEvaluator.ScoreRelic(id, plan, snapshot);

        var characterId = snapshot?["characterId"]?.GetValue<string>();
        score += CodexPriorCatalog.GetRelicBonus(characterId, id, RelicContext(snapshot));

        return score;
    }

    static string RelicContext(JsonObject? snapshot) {
        var ctx = snapshot?["relicChoiceContext"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(ctx))
            return ctx;

        var roomType = snapshot?["roomType"]?.GetValue<string>() ?? "";
        if (roomType.Contains("Event", StringComparison.OrdinalIgnoreCase))
            return "event";

        var eventId = snapshot?["eventId"]?.GetValue<string>() ?? "";
        if (eventId.Contains("NEOW", StringComparison.OrdinalIgnoreCase))
            return "event";
        return "combat_reward";
    }

    static bool IsOwned(string id, string name, JsonArray? ownedRelics) {
        if (ownedRelics == null) return false;
        foreach (var node in ownedRelics) {
            if (node is JsonObject o) {
                if (!string.IsNullOrWhiteSpace(id)
                    && string.Equals(o["id"]?.GetValue<string>(), id, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(o["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (string.Equals(node?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
