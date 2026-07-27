using System.Text.Json.Nodes;

namespace KitLib.AI.Planning;

/// <summary>Marginal deck value from combat simulation on the planned route (not community priors).</summary>
public static class DeckSimScorer {
    /// <summary>Route-weighted fight value for the current deck snapshot.</summary>
    public static int ScoreDeck(JsonObject snapshot, DeckPlan plan) =>
        NextFightDeckEvaluator.GetBaselineRouteScore(snapshot, plan);

    /// <summary>Δ fight value from hypothetically adding one card to the deck.</summary>
    public static int MarginalCardDelta(JsonObject snapshot, JsonObject offeredCard, DeckPlan plan) =>
        NextFightDeckEvaluator.ScoreOfferDelta(snapshot, offeredCard, plan);

    /// <summary>Δ fight value from hypothetically removing one deck card (by snapshot card index).</summary>
    public static int RemovalDelta(JsonObject snapshot, DeckPlan plan, int cardIndex) {
        if (!HasRoutePreview(snapshot))
            return 0;

        var without = WithoutCardAtIndex(snapshot, cardIndex);
        return ScoreDeck(without, plan) - ScoreDeck(snapshot, plan);
    }

    public static bool HasRoutePreview(JsonObject snapshot) =>
        NextFightRoute.ResolveFromSnapshot(snapshot).Count > 0;

    static JsonObject WithoutCardAtIndex(JsonObject snapshot, int cardIndex) {
        var clone = snapshot.DeepClone() as JsonObject ?? new JsonObject();
        var deck = clone["deck"]?.AsArray();
        if (deck == null)
            return clone;

        var newDeck = new JsonArray();
        foreach (var node in deck) {
            if (node is not JsonObject card)
                continue;
            if ((card["index"]?.GetValue<int>() ?? -1) == cardIndex)
                continue;
            newDeck.Add(node.DeepClone());
        }

        clone["deck"] = newDeck;
        return clone;
    }
}
