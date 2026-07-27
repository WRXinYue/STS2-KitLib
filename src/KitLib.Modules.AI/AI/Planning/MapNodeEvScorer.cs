using System;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.AI.Planning;

/// <summary>Sim-first map node EV replacing static type heuristics.</summary>
public static class MapNodeEvScorer {
    public static int ScoreNode(
        MapPointType type,
        MapRouteContext ctx,
        JsonObject snapshot,
        NextFightNode? combatFight = null,
        bool forRouteDp = false) {
        var plan = ctx.Plan;

        switch (type) {
            case MapPointType.RestSite:
                return forRouteDp
                    ? ScoreRestHeuristic(ctx)
                    : ScoreRest(ctx, snapshot, plan);

            case MapPointType.Elite:
            case MapPointType.Monster:
            case MapPointType.Boss:
                if (forRouteDp)
                    return ScoreCombatFallback(type, ctx);
                return combatFight != null
                    ? ScoreCombat(snapshot, plan, combatFight)
                    : ScoreCombatFallback(type, ctx);

            case MapPointType.Treasure:
                return ScoreTreasure(ctx);

            case MapPointType.Shop:
                return ScoreShop(ctx);

            case MapPointType.Unknown:
                return (ScoreCombatFallback(MapPointType.Monster, ctx) * 7
                    + ScoreEvent(ctx, snapshot, plan)) / 10;

            case MapPointType.Ancient:
                return ScoreEvent(ctx, snapshot, plan);

            default:
                return 0;
        }
    }

    static int ScoreRest(MapRouteContext ctx, JsonObject snapshot, DeckPlan plan) {
        var ev = RestEvScorer.Evaluate(snapshot);
        int score = Math.Max(ev.HealEv, ev.SmithEv);

        if (score <= 0) {
            score = ctx.HpRatio switch {
                < 0.55f => 28,
                < 0.75f => 14,
                _ => 6,
            };
            score += MapUpgradeEvaluator.RestRouteBonus(ctx) / 2;
        }

        return Math.Clamp(score, 4, 48);
    }

    static int ScoreRestHeuristic(MapRouteContext ctx) {
        int score = ctx.HpRatio switch {
            < 0.55f => 28,
            < 0.75f => 14,
            _ => 6,
        };
        score += MapUpgradeEvaluator.RestRouteBonus(ctx) / 2;
        return Math.Clamp(score, 4, 48);
    }

    static int ScoreCombat(JsonObject snapshot, DeckPlan plan, NextFightNode fight) {
        int net = EliteEvEstimator.EstimateNetEv(snapshot, plan, fight);
        if (fight.RoomType == RoomType.Monster)
            net = Math.Max(net, EliteEvEstimator.EstimateRewardEv(snapshot, plan, fight.RoomType)
                - EliteEvEstimator.EstimateFightCost(snapshot, fight) / 2);

        return Math.Clamp(net, -40, 45);
    }

    static int ScoreCombatFallback(MapPointType type, MapRouteContext ctx) {
        if (type == MapPointType.Elite) {
            int score = ctx.HpRatio > 0.6f ? 10 : -8;
            if (ctx.RouteFightScore >= 15)
                score += 8;
            if (ctx.HpRatio < 0.5f)
                score -= 15;
            return score;
        }

        return type == MapPointType.Boss ? 5 : 6;
    }

    static int ScoreTreasure(MapRouteContext ctx) {
        int score = DeckCardScoring.RarityScore("UNCOMMON");
        if (ctx.HpRatio < 0.45f)
            score -= 8;
        return score;
    }

    static int ScoreShop(MapRouteContext ctx) {
        int score = 8;
        if (ctx.WantsShopRemoval)
            score += 22;
        if (ctx.Gold < 50)
            score -= 12;
        return score;
    }

    static int ScoreEvent(MapRouteContext ctx, JsonObject snapshot, DeckPlan plan) {
        int score = 10 + (int)(ctx.Plan.ThinPreference * 6f);
        if (ctx.BestUpgradeScore >= MapUpgradeEvaluator.CriticalUpgradeThreshold && ctx.HpRatio >= 0.6f)
            score -= 6;
        return score;
    }
}
