using System;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Planning;

public sealed record DeckComposition(int StrikeCount, int DefendCount, int CurseCount);

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
        int strikes = 0, defends = 0, curses = 0;
        foreach (var node in deck) {
            if (node is not JsonObject card) continue;
            var id = (card["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
            var rarity = (card["rarity"]?.GetValue<string>() ?? "").ToUpperInvariant();
            if (id.Contains("STRIKE", StringComparison.Ordinal)) strikes++;
            else if (id.Contains("DEFEND", StringComparison.Ordinal)) defends++;
            if (rarity.Contains("CURSE", StringComparison.Ordinal)) curses++;
        }
        return new DeckComposition(strikes, defends, curses);
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
}
