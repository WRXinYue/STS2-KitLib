using MegaCrit.Sts2.Core.Map;

namespace DevMode.AI.Planning;

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

    public static int EdgeBonus(MapPointType from, MapPointType to, MapRouteContext ctx) {
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

        return bonus;
    }

    static int ScoreRest(MapRouteContext ctx) {
        if (ctx.HpRatio < 0.55f) return 38;
        if (ctx.HpRatio < 0.75f) return 22;
        if (ctx.Metrics.MeanValue >= 10f) return 10;
        return 4;
    }

    static int ScoreShop(MapRouteContext ctx) {
        int score = ctx.HpRatio > 0.45f ? 12 : 4;
        if (ctx.WantsShopRemoval) score += 28;
        if (ctx.Metrics.StarterBloat >= 2) score += 12;
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
        return score;
    }

    static int ScoreUnknown(MapRouteContext ctx) {
        int monster = ScoreMonster(ctx);
        int evt = ScoreEvent(ctx);
        return (int)(monster * 0.7f + evt * 0.3f);
    }
}
