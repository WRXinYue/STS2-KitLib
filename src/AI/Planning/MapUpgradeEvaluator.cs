using System;
using System.Text.Json.Nodes;

namespace KitLib.AI.Planning;

/// <summary>Estimates smith value from the current deck for map routing and rest-site choices.</summary>
public static class MapUpgradeEvaluator {
    public const int StrongUpgradeThreshold = 30;
    public const int CriticalUpgradeThreshold = 45;

    public static int BestDeckUpgradeScore(JsonObject snapshot, DeckPlan plan) {
        var deck = snapshot["deck"]?.AsArray();
        if (deck == null || deck.Count == 0)
            return 0;

        var composition = DeckCardScoring.AnalyzeComposition(deck);
        int best = 0;

        foreach (var node in deck) {
            if (node is not JsonObject card) continue;
            var upgrade = card["upgradeLevel"]?.GetValue<int>() ?? 0;
            var maxUpgrade = card["maxUpgradeLevel"]?.GetValue<int>() ?? 1;
            if (upgrade >= maxUpgrade) continue;

            best = Math.Max(best, DeckCardScoring.ScoreUpgradeCandidate(card, plan, composition, snapshot));
        }

        return best;
    }

    public static bool HasUpgradeTarget(JsonObject snapshot, DeckPlan plan) =>
        BestDeckUpgradeScore(snapshot, plan) > 0;

    public static int RestRouteBonus(MapRouteContext ctx) {
        if (ctx.BestUpgradeScore < StrongUpgradeThreshold)
            return 0;

        int bonus = Math.Min(32, ctx.BestUpgradeScore / 2);
        if (ctx.HpRatio >= 0.55f)
            bonus += 6;
        if (ctx.BestUpgradeScore >= CriticalUpgradeThreshold && ctx.HpRatio >= 0.65f)
            bonus += 10;

        return bonus;
    }
}
