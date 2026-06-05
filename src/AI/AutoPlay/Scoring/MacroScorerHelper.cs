using System;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;
using DevMode.AI.Planning;

namespace DevMode.AI.AutoPlay.Scoring;

internal static class MacroScorerHelper {
    public static int RarityScore(string? rarity) => DeckCardScoring.RarityScore(rarity);

    public static int ScoreCardOffer(JsonObject card, DeckPlan plan, int deckSize, JsonObject? snapshot = null) {
        var score = DeckCardScoring.ScoreInDeck(card, plan, new DeckComposition(0, 0, 0));
        score -= (int)Math.Round(DeckPlanInferer.DilutionPenalty(deckSize + 1, plan));
        var characterId = snapshot?["characterId"]?.GetValue<string>();
        var cardId = card["id"]?.GetValue<string>();
        var context = snapshot?["shopOffers"] != null ? "shop" : "combat_reward";
        score += CodexPriorCatalog.GetCardBonus(characterId, cardId, context);
        return score;
    }

    public static int ScoreRelicOffer(JsonObject relic, DeckPlan plan, JsonArray? ownedRelics, JsonObject? snapshot = null) {
        var id = relic["id"]?.GetValue<string>() ?? "";
        var name = relic["name"]?.GetValue<string>() ?? "";

        if (IsOwned(id, name, ownedRelics))
            return -100;

        var score = RarityScore(relic["rarity"]?.GetValue<string>());
        foreach (var tag in RelicCatalog.ResolveTags(id))
            score += (int)Math.Round(plan.GetWeight(tag) * 3f);

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
