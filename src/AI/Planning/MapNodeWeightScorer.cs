using System;
using MegaCrit.Sts2.Core.Map;

namespace KitLib.AI.Planning;

public static class MapNodeWeightScorer {
    public static int ScoreNode(MapPointType type, MapRouteContext ctx) {
        return type switch {
            MapPointType.RestSite => ScoreRest(ctx),
            MapPointType.Shop => ScoreShop(ctx),
            MapPointType.Elite => ScoreElite(ctx),
            MapPointType.Monster => ScoreMonster(ctx),
            MapPointType.Treasure => ScoreTreasure(ctx),
            MapPointType.Unknown => ScoreUnknown(ctx),
            MapPointType.Boss => 0,
            MapPointType.Ancient => ScoreUnknown(ctx),
            _ => 0,
        };
    }

    public static int PathRiskNodeAdjust(MapPointType type, int pathRisk, MapRouteContext ctx, int elitesToRest) {
        if (pathRisk <= 0) return 0;

        int adjust = type switch {
            MapPointType.RestSite => Math.Min(pathRisk, 40),
            MapPointType.Elite => -Math.Min(pathRisk * 2 / 3, 28),
            MapPointType.Monster => pathRisk >= 25 ? -6 : 0,
            MapPointType.Shop => pathRisk >= 30 && ctx.HpRatio < 0.6f ? -8 : 0,
            MapPointType.Unknown => pathRisk >= 25 ? -4 : 0,
            _ => 0,
        };

        if (type == MapPointType.Elite && elitesToRest >= 2)
            adjust -= 10;

        return adjust;
    }

    public static int EdgeBonus(MapPointType from, MapPointType to, MapRouteContext ctx, int pathRisk = 0) {
        int bonus = 0;

        if (from == MapPointType.RestSite && to == MapPointType.Elite && ctx.HpRatio < 0.7f)
            bonus -= 10;

        if (from == MapPointType.Shop && ctx.WantsShopRemoval)
            bonus += 6;

        if (from == MapPointType.Elite && to == MapPointType.Elite && ctx.HpRatio < 0.5f)
            bonus -= 12;

        if (from == MapPointType.RestSite && to == MapPointType.RestSite)
            bonus -= 5;

        if (from == MapPointType.Treasure && to == MapPointType.Elite)
            bonus += 4;

        if (pathRisk > 0) {
            if (from == MapPointType.RestSite && to == MapPointType.Elite)
                bonus -= pathRisk / 4;
            if (from == MapPointType.Elite && to == MapPointType.Elite)
                bonus -= pathRisk / 3;
        }

        return bonus;
    }

    static int ScoreRest(MapRouteContext ctx) {
        int score = ctx.HpRatio switch {
            < 0.55f => 38,
            < 0.75f => 22,
            _ when ctx.Metrics.MeanValue >= 10f => 10,
            _ => 4,
        };

        score += MapUpgradeEvaluator.RestRouteBonus(ctx);
        return score;
    }

    static int ScoreShop(MapRouteContext ctx) {
        int score = ctx.HpRatio > 0.45f ? 12 : 4;
        if (ctx.WantsShopRemoval) score += 28;
        if (ctx.Metrics.StarterBloat >= 2) score += 12;
        if (ctx.Metrics.StrikeSurplus >= 2) score += 10;
        if (ctx.Metrics.CardsNeedingBurn >= 4) score += 14;
        if (ctx.Gold < 50) score -= 15;
        return score;
    }

    static int ScoreElite(MapRouteContext ctx) {
        int score = 8;
        if (ctx.HpRatio > 0.6f) score += 16;
        if (ctx.Metrics.MeanValue >= 12f) score += 10;
        if (ctx.HpRatio < 0.5f) score -= 22;
        if (ctx.Ascension >= 7 && ctx.HpRatio < 0.7f) score -= 12;
        if (ctx.TotalFloor < 6) score += 4;
        if (ctx.ActIndex == 0 && ctx.TotalFloor is >= 6 and <= 9 && ctx.HpRatio < 0.75f)
            score -= 15;
        return score;
    }

    static int ScoreMonster(MapRouteContext ctx) {
        int score = 10;
        if (ctx.Gold < 80) score += 4;
        if (ctx.HpRatio < 0.4f) score -= 6;
        return score;
    }

    static int ScoreTreasure(MapRouteContext ctx) => 20;

    static int ScoreEvent(MapRouteContext ctx) {
        int score = 12;
        score += (int)(ctx.Plan.ThinPreference * 8f);
        if (ctx.BestUpgradeScore >= MapUpgradeEvaluator.StrongUpgradeThreshold && ctx.HpRatio >= 0.55f)
            score -= 6;
        return score;
    }

    static int ScoreUnknown(MapRouteContext ctx) {
        int monster = ScoreMonster(ctx);
        int evt = ScoreEvent(ctx);
        int score = (int)(monster * 0.7f + evt * 0.3f);
        if (ctx.BestUpgradeScore >= MapUpgradeEvaluator.CriticalUpgradeThreshold && ctx.HpRatio >= 0.6f)
            score -= 8;
        return score;
    }
}
