using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

/// <summary>Route fight value plus HP buffer for macro rest / path EV.</summary>
public static class RouteSimScorer {
    public const float RestHealFraction = 0.30f;

    public static int RouteValue(JsonObject snapshot, DeckPlan plan, IReadOnlyList<NextFightNode> route) {
        int fight = NextFightDeckEvaluator.ScoreRoute(snapshot, plan, route);
        int hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        return fight + hp / 4;
    }

    public static JsonObject WithHeal(JsonObject snapshot) {
        var clone = snapshot.DeepClone() as JsonObject ?? new JsonObject();
        int maxHp = clone["maxHp"]?.GetValue<int>() ?? 1;
        int hp = clone["currentHp"]?.GetValue<int>() ?? 0;
        int heal = ComputeHealAmount(maxHp);
        clone["currentHp"] = Math.Min(maxHp, hp + heal);
        return clone;
    }

    public static int ComputeHealAmount(int maxHp) =>
        Math.Max(1, (int)Math.Round(maxHp * RestHealFraction));

    public static JsonObject? WithBestUpgrade(JsonObject snapshot, DeckPlan plan) {
        var target = FindBestUpgradeTarget(snapshot, plan);
        if (target == null)
            return null;

        var clone = snapshot.DeepClone() as JsonObject ?? new JsonObject();
        var deck = clone["deck"]?.AsArray();
        if (deck == null)
            return null;

        foreach (var node in deck) {
            if (node is not JsonObject card)
                continue;
            if ((card["index"]?.GetValue<int>() ?? -1) != target.CardIndex)
                continue;

            int level = card["upgradeLevel"]?.GetValue<int>() ?? 0;
            int maxLevel = card["maxUpgradeLevel"]?.GetValue<int>() ?? 1;
            card["upgradeLevel"] = Math.Min(maxLevel, level + 1);
            return clone;
        }

        return null;
    }

    public static UpgradeTarget? FindBestUpgradeTarget(JsonObject snapshot, DeckPlan plan) {
        var deck = snapshot["deck"]?.AsArray();
        if (deck == null || deck.Count == 0)
            return null;

        var composition = DeckCardScoring.AnalyzeComposition(deck);
        int bestScore = 0;
        int bestIndex = -1;
        string bestId = "";

        foreach (var node in deck) {
            if (node is not JsonObject card)
                continue;

            int upgrade = card["upgradeLevel"]?.GetValue<int>() ?? 0;
            int maxUpgrade = card["maxUpgradeLevel"]?.GetValue<int>() ?? 1;
            if (upgrade >= maxUpgrade)
                continue;

            int score = DeckCardScoring.ScoreUpgradeCandidate(card, plan, composition, snapshot);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestIndex = card["index"]?.GetValue<int>() ?? -1;
            bestId = card["id"]?.GetValue<string>() ?? "";
        }

        if (bestIndex < 0 || bestScore <= 0)
            return null;

        return new UpgradeTarget(bestIndex, bestId, bestScore);
    }

    public sealed record UpgradeTarget(int CardIndex, string CardId, int UpgradeScore);
}
