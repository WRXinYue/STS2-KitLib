using System;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

public sealed record DeckMetrics(
    int DeckSize,
    int TotalValue,
    float MeanValue,
    int WorstValue,
    int WorstCardIndex,
    string WorstCardName,
    int RemovalUplift,
    int StarterBloat,
    float ConsistencyScore,
    /// <summary>Strikes above <see cref="DeckPlan.TargetStrikeCount"/> — priority removal targets.</summary>
    int StrikeSurplus,
    /// <summary>Defends above <see cref="DeckPlan.TargetDefendCount"/>.</summary>
    int DefendSurplus,
    /// <summary>Cards over <see cref="DeckPlan.TargetDeckSize"/>.</summary>
    int ThinGap,
    int ExhaustCount,
    /// <summary>
    /// Macro cards still worth removing (shop / exhaust events): thin gap + starter surplus +
    /// non-exhaust filler an exhaust deck cannot burn fast enough.
    /// </summary>
    int CardsNeedingBurn,
    int BlockSourceCount,
    int DrawSourceCount,
    int BlockDeficit,
    int DrawDeficit,
    /// <summary>BlockDeficit×2 + DrawDeficit — macro survivability gap.</summary>
    int SurvivalGap);

/// <summary>Evaluates deck quality and marginal benefit of removing the worst card.</summary>
public static class DeckEvaluator {
    public const int MinRemovalUplift = 11;

    public static DeckMetrics Evaluate(JsonObject snapshot, DeckPlan plan) {
        var deck = snapshot["deck"]?.AsArray();
        var actIndex = snapshot["actIndex"]?.GetValue<int>() ?? 0;
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;

        if (deck == null || deck.Count == 0) {
            return EmptyMetrics();
        }

        var composition = DeckCardScoring.AnalyzeComposition(deck);
        var survivability = DeckSurvivability.CountSources(deck);
        int blockDeficit = DeckSurvivability.BlockDeficit(plan, survivability.BlockSourceCount);
        int drawDeficit = DeckSurvivability.DrawDeficit(plan, survivability.DrawSourceCount);
        int survivalGap = DeckSurvivability.SurvivalGap(plan, survivability);
        int total = 0;
        int worstValue = int.MaxValue;
        int worstIndex = 0;
        string worstName = "";
        int nonExhaustFiller = 0;

        for (int i = 0; i < deck.Count; i++) {
            if (deck[i] is not JsonObject card) continue;
            int value = DeckCardScoring.ScoreInDeck(card, plan, composition);
            total += value;

            if (IsNonExhaustFiller(card, plan, composition, value))
                nonExhaustFiller++;

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

        int strikeSurplus = Math.Max(0, composition.StrikeCount - plan.TargetStrikeCount);
        int defendSurplus = Math.Max(0, composition.DefendCount - plan.TargetDefendCount);
        int thinGap = Math.Max(0, deckSize - plan.TargetDeckSize);
        int starterBloat = strikeSurplus
            + (int)Math.Round(defendSurplus * 0.8f)
            + composition.CurseCount * 3;
        int cardsNeedingBurn = ComputeCardsNeedingBurn(
            thinGap, strikeSurplus, defendSurplus, nonExhaustFiller,
            composition.ExhaustCount, plan);

        int starterBloatBonus = (int)Math.Round(starterBloat * 4f);
        int dilution = (int)Math.Round(DeckPlanInferer.DilutionPenalty(deckSize, plan));
        int futureThin = FutureThinBonus(actIndex, floor);
        int burnPressure = (int)Math.Round(cardsNeedingBurn * 1.5f);

        int removalUplift = (int)Math.Round(mean - worstValue)
            + starterBloatBonus
            + dilution
            + futureThin
            + burnPressure;

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
            consistency,
            strikeSurplus,
            defendSurplus,
            thinGap,
            composition.ExhaustCount,
            cardsNeedingBurn,
            survivability.BlockSourceCount,
            survivability.DrawSourceCount,
            blockDeficit,
            drawDeficit,
            survivalGap);
    }

    static DeckMetrics EmptyMetrics() =>
        new(0, 0, 0, 0, -1, "", 0, 0, 1f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    static bool IsNonExhaustFiller(
        JsonObject card,
        DeckPlan plan,
        DeckComposition composition,
        int scoreInDeck) {
        var tags = CardCatalog.ResolveTags(
            card["id"]?.GetValue<string>(),
            card["cardType"]?.GetValue<string>(),
            card["keywords"]?.AsArray());
        if (tags.Contains(AiTag.Exhaust)) return false;

        var id = (card["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
        var rarity = (card["rarity"]?.GetValue<string>() ?? "").ToUpperInvariant();
        if (id.Contains("STRIKE", StringComparison.Ordinal)
            || id.Contains("DEFEND", StringComparison.Ordinal)
            || rarity.Contains("CURSE", StringComparison.Ordinal))
            return true;

        return scoreInDeck < 6 && plan.GetWeight(AiTag.Exhaust) >= 0.8f;
    }

    static int ComputeCardsNeedingBurn(
        int thinGap,
        int strikeSurplus,
        int defendSurplus,
        int nonExhaustFiller,
        int exhaustCount,
        DeckPlan plan) {
        int burn = thinGap + strikeSurplus + defendSurplus;

        if (!plan.IsExhaustFocused)
            return burn;

        // Macro: each exhaust card covers ~2 filler cards over a run; debt = remainder.
        int exhaustRelief = exhaustCount * 2;
        burn += Math.Max(0, nonExhaustFiller - exhaustRelief);
        return burn;
    }

    static int FutureThinBonus(int actIndex, int floor) => actIndex switch {
        0 => floor < 20 ? 5 : 3,
        1 => 2,
        _ => 0,
    };

    /// <summary>Single scalar for comparing deck states (higher = stronger deck).</summary>
    public static int DeckQualityScore(DeckMetrics metrics, DeckPlan plan) {
        int score = metrics.TotalValue;
        score += (int)Math.Round(metrics.MeanValue * 2f);
        score -= (int)Math.Round(DeckPlanInferer.DilutionPenalty(metrics.DeckSize, plan) * 10f);
        score -= metrics.SurvivalGap * 3;
        score -= metrics.StarterBloat * 3;
        score += (int)Math.Round(metrics.ConsistencyScore * 10f);
        if (plan.IsExhaustFocused)
            score -= metrics.CardsNeedingBurn * 2;
        return score;
    }

    /// <summary>
    /// Opportunity cost of adding one more card — derived from the same quality model as marginal picks.
    /// </summary>
    public static int SkipOpportunityCost(DeckMetrics metrics, DeckPlan plan, JsonObject? snapshot = null) {
        int score = (int)Math.Round(plan.ThinPreference * 14f);
        score += (int)Math.Round(DeckPlanInferer.DilutionPenalty(metrics.DeckSize, plan) * 8f);
        score += metrics.ThinGap * 3;
        score += Math.Max(0, metrics.DeckSize - plan.TargetDeckSize) * 2;

        if (metrics.DeckSize > 0) {
            int qualityPerCard = DeckQualityScore(metrics, plan) / metrics.DeckSize;
            score += Math.Max(0, qualityPerCard - 12) * 2;
        }

        if (metrics.SurvivalGap == 0)
            score += 5;

        if (plan.IsExhaustFocused && metrics.CardsNeedingBurn >= 5)
            score += 8;

        if (snapshot != null) {
            var actIndex = snapshot["actIndex"]?.GetValue<int>() ?? 0;
            var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
            if (actIndex >= 2 && metrics.DeckSize > plan.TargetDeckSize + 2)
                score += 15;
            if (floor > 30 && metrics.DeckSize > 20)
                score += 10;
            if (metrics.DeckSize > plan.TargetDeckSize + 5)
                score += 12;
        }

        if (metrics.StarterBloat >= 3)
            score -= 8;
        if (metrics.StrikeSurplus >= 3)
            score -= 10;

        if (metrics.BlockDeficit >= 2 && metrics.SurvivalGap >= 2)
            score -= 6;
        else if (metrics.BlockDeficit >= 3)
            score -= 6;

        if (metrics.DrawDeficit >= 2 && metrics.DeckSize <= plan.TargetDeckSize)
            score -= 4;

        return Math.Max(0, score);
    }

    /// <summary>Quality delta from hypothetically adding one offered card to the deck.</summary>
    public static int MarginalPickScore(JsonObject snapshot, DeckPlan plan, JsonObject offeredCard) {
        var before = Evaluate(snapshot, plan);
        var after = Evaluate(WithAddedCard(snapshot, offeredCard), plan);
        return DeckQualityScore(after, plan) - DeckQualityScore(before, plan);
    }

    public static bool HasTransformCore(JsonArray? deck) {
        if (deck == null) return false;
        foreach (var node in deck) {
            if (node is not JsonObject card) continue;
            var profile = CardMechanicIndex.InferFromSnapshot(card);
            if (profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)
                || profile.Flags.HasFlag(CardMechanicFlags.TransformsCards))
                return true;
        }
        return false;
    }

    static JsonObject WithAddedCard(JsonObject snapshot, JsonObject card) {
        var clone = snapshot.DeepClone() as JsonObject ?? new JsonObject();
        var deck = clone["deck"]?.AsArray() ?? new JsonArray();
        var newDeck = new JsonArray();
        foreach (var node in deck) {
            if (node != null)
                newDeck.Add(node.DeepClone());
        }
        newDeck.Add(card.DeepClone());
        clone["deck"] = newDeck;
        return clone;
    }
}
