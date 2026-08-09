using System;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

public sealed record DeckComposition(
    int StrikeCount,
    int DefendCount,
    int CurseCount,
    int ExhaustCount);

/// <summary>Scores cards already in the deck (no dilution penalty).</summary>
public static class DeckCardScoring {
    public static int RarityScore(string? rarity) => rarity?.ToUpperInvariant() switch {
        "RARE" => 25,
        "UNCOMMON" => 15,
        "COMMON" => 8,
        "STARTER" => 3,
        "ANCIENT" => 30,
        "EVENT" => 12,
        _ => 5,
    };

    public static DeckComposition AnalyzeComposition(JsonArray deck) {
        int strikes = 0, defends = 0, curses = 0, exhaust = 0;
        foreach (var node in deck) {
            if (node is not JsonObject card) continue;
            var id = (card["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
            var rarity = (card["rarity"]?.GetValue<string>() ?? "").ToUpperInvariant();
            if (id.Contains("STRIKE", StringComparison.Ordinal)) strikes++;
            else if (id.Contains("DEFEND", StringComparison.Ordinal)) defends++;
            if (rarity.Contains("CURSE", StringComparison.Ordinal)) curses++;

            var tags = CardCatalog.ResolveTags(
                card["id"]?.GetValue<string>(),
                card["cardType"]?.GetValue<string>(),
                card["keywords"]?.AsArray());
            if (tags.Contains(AiTag.Exhaust)) exhaust++;
        }
        return new DeckComposition(strikes, defends, curses, exhaust);
    }

    public static int ScoreInDeck(JsonObject card, DeckPlan plan, DeckComposition composition) {
        var id = card["id"]?.GetValue<string>() ?? "";
        var idUpper = id.ToUpperInvariant();
        var rarity = card["rarity"]?.GetValue<string>() ?? "";
        var rarityUpper = rarity.ToUpperInvariant();
        var tags = CardCatalog.ResolveTags(id, card["cardType"]?.GetValue<string>(), card["keywords"]?.AsArray());
        var tagScore = DeckPlanInferer.ScoreTags(tags, plan);

        int score = (int)Math.Round(tagScore);
        score += RarityScore(rarity);

        var cost = card["cost"]?.GetValue<int>() ?? 1;
        if (cost == 0) score += 8;
        if (cost >= 3) score -= 3;

        var upgrade = card["upgradeLevel"]?.GetValue<int>() ?? 0;
        if (upgrade > 0) score += 5;

        if (rarityUpper.Contains("CURSE", StringComparison.Ordinal))
            score -= 40;

        if (idUpper.Contains("STRIKE", StringComparison.Ordinal))
            score -= 15 * Math.Max(0, composition.StrikeCount - 1);

        if (idUpper.Contains("DEFEND", StringComparison.Ordinal))
            score -= 12 * Math.Max(0, composition.DefendCount - 1);

        if (rarityUpper.Contains("STARTER", StringComparison.Ordinal) && tagScore < 1f)
            score -= 10;

        if (cost == 0 && tagScore < 0.8f)
            score -= 5;

        return score;
    }

    /// <summary>Smith / in-combat upgrade prompts — favors core mechanics over strikes.</summary>
    public static int ScoreUpgradeCandidate(
        JsonObject card,
        DeckPlan plan,
        DeckComposition composition,
        JsonObject? snapshot = null) {
        var idUpper = (card["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
        int score = ScoreInDeck(card, plan, composition);

        if (snapshot != null)
            score += DeckSynergyEvaluator.ScoreCard(card, plan, snapshot);

        var profile = CardMechanicIndex.InferFromSnapshot(card);
        if (profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks))
            score += 45;
        else if (profile.Flags.HasFlag(CardMechanicFlags.TransformsCards))
            score += 25;
        if (profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable))
            score += 12;
        if (profile.Flags.HasFlag(CardMechanicFlags.Aoe))
            score += 8;

        if (idUpper.Contains("STRIKE", StringComparison.Ordinal))
            score -= 50;
        else if (idUpper.Contains("DEFEND", StringComparison.Ordinal))
            score -= 35;

        var rarityUpper = (card["rarity"]?.GetValue<string>() ?? "").ToUpperInvariant();
        if (rarityUpper.Contains("BASIC", StringComparison.Ordinal)
            || rarityUpper.Contains("STARTER", StringComparison.Ordinal))
            score -= 18;

        return score;
    }
}
