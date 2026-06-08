using System;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

public sealed record SurvivabilityCounts(int BlockSourceCount, int DrawSourceCount);

/// <summary>Counts transitional block and draw sources for macro deck survivability.</summary>
public static class DeckSurvivability {
    public const int TransitionBlockCostMax = 2;

    public static SurvivabilityCounts CountSources(JsonArray deck) {
        int blocks = 0, draws = 0;
        foreach (var node in deck) {
            if (node is not JsonObject card) continue;
            if (IsTransitionBlock(card)) blocks++;
            if (IsDrawSource(card)) draws++;
        }
        return new SurvivabilityCounts(blocks, draws);
    }

    public static int BlockDeficit(DeckPlan plan, int blockSourceCount) =>
        Math.Max(0, plan.TargetBlockSources - blockSourceCount);

    public static int DrawDeficit(DeckPlan plan, int drawSourceCount) =>
        Math.Max(0, plan.TargetDrawSources - drawSourceCount);

    public static int SurvivalGap(DeckPlan plan, SurvivabilityCounts counts) =>
        BlockDeficit(plan, counts.BlockSourceCount) * 2
        + DrawDeficit(plan, counts.DrawSourceCount);

    public static bool IsTransitionBlock(JsonObject card) {
        if (!HasBlock(card)) return false;

        var id = (card["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
        var rarity = (card["rarity"]?.GetValue<string>() ?? "").ToUpperInvariant();
        if (rarity.Contains("STARTER", StringComparison.Ordinal)
            || id.Contains("DEFEND", StringComparison.Ordinal))
            return false;

        var cost = card["cost"]?.GetValue<int>() ?? 1;
        return cost <= TransitionBlockCostMax;
    }

    public static bool IsDrawSource(JsonObject card) {
        var rarity = (card["rarity"]?.GetValue<string>() ?? "").ToUpperInvariant();
        if (rarity.Contains("STARTER", StringComparison.Ordinal))
            return false;

        var id = card["id"]?.GetValue<string>();
        var tags = CardCatalog.ResolveTags(
            id,
            card["cardType"]?.GetValue<string>(),
            card["keywords"]?.AsArray());
        if (tags.Contains(AiTag.Draw)) return true;

        if (CardMechanicIndex.TryGet(id, out var profile))
            return profile.Flags.HasFlag(CardMechanicFlags.HasDraw);

        return false;
    }

    public static int ScoreTransitionBlockOffer(JsonObject card, DeckPlan plan, DeckMetrics metrics) {
        if (!IsTransitionBlock(card) || metrics.BlockDeficit <= 0) return 0;
        return 6 + metrics.BlockDeficit * 4;
    }

    public static int ScoreDrawSourceOffer(JsonObject card, DeckPlan plan, DeckMetrics metrics) {
        if (!IsDrawSource(card) || metrics.DrawDeficit <= 0) return 0;
        return 4 + metrics.DrawDeficit * 3;
    }

    static bool HasBlock(JsonObject card) {
        if (card["block"]?.GetValue<int>() is > 0) return true;

        var id = card["id"]?.GetValue<string>();
        var tags = CardCatalog.ResolveTags(
            id,
            card["cardType"]?.GetValue<string>(),
            card["keywords"]?.AsArray());
        if (tags.Contains(AiTag.Block)) return true;

        if (CardMechanicIndex.TryGet(id, out var profile))
            return profile.Flags.HasFlag(CardMechanicFlags.HasBlock);

        return false;
    }
}
