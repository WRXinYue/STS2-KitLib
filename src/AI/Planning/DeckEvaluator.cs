using System;
using System.Text.Json.Nodes;

namespace DevMode.AI.Planning;

public sealed record DeckMetrics(
    int DeckSize,
    int TotalValue,
    float MeanValue,
    int WorstValue,
    int WorstCardIndex,
    string WorstCardName,
    int RemovalUplift,
    int StarterBloat,
    float ConsistencyScore
);

/// <summary>Evaluates deck quality and marginal benefit of removing the worst card.</summary>
public static class DeckEvaluator {
    public const int MinRemovalUplift = 11;

    public static DeckMetrics Evaluate(JsonObject snapshot, DeckPlan plan) {
        var deck = snapshot["deck"]?.AsArray();
        var actIndex = snapshot["actIndex"]?.GetValue<int>() ?? 0;
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;

        if (deck == null || deck.Count == 0) {
            return new DeckMetrics(0, 0, 0, 0, -1, "", 0, 0, 1f);
        }

        var composition = DeckCardScoring.AnalyzeComposition(deck);
        int total = 0;
        int worstValue = int.MaxValue;
        int worstIndex = 0;
        string worstName = "";

        for (int i = 0; i < deck.Count; i++) {
            if (deck[i] is not JsonObject card) continue;
            int value = DeckCardScoring.ScoreInDeck(card, plan, composition);
            total += value;

            if (value < worstValue) {
                worstValue = value;
                worstIndex = card["index"]?.GetValue<int>() ?? i;
                worstName = card["name"]?.GetValue<string>() ?? $"card {worstIndex}";
            }
        }

        if (worstValue == int.MaxValue)
            worstValue = 0;

        int deckSize = deck.Count;
        float mean = deckSize > 0 ? (float)total / deckSize : 0f;
        int starterBloat = ComputeStarterBloat(composition);
        int starterBloatBonus = (int)Math.Round(starterBloat * 4f);
        int dilution = (int)Math.Round(DeckPlanInferer.DilutionPenalty(deckSize, plan));
        int futureThin = FutureThinBonus(actIndex, floor);

        int removalUplift = (int)Math.Round(mean - worstValue)
            + starterBloatBonus
            + dilution
            + futureThin;

        float consistency = deckSize > 0
            ? Math.Clamp(1f - starterBloat / (float)Math.Max(deckSize, 1), 0f, 1f)
            : 1f;

        return new DeckMetrics(
            deckSize,
            total,
            mean,
            worstValue,
            worstIndex,
            worstName,
            removalUplift,
            starterBloat,
            consistency);
    }

    static int ComputeStarterBloat(DeckComposition composition) {
        int bloat = 0;
        if (composition.StrikeCount > 2)
            bloat += composition.StrikeCount - 2;
        if (composition.DefendCount > 2)
            bloat += (int)Math.Round((composition.DefendCount - 2) * 0.8f);
        bloat += composition.CurseCount * 3;
        return bloat;
    }

    static int FutureThinBonus(int actIndex, int floor) => actIndex switch {
        0 => floor < 20 ? 5 : 3,
        1 => 2,
        _ => 0,
    };
}
