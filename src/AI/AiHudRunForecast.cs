using System;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Planning;
using DevMode.AI.Sts2;

namespace DevMode.AI;

/// <summary>Heuristic deck profile and run prognosis for the in-game HUD.</summary>
public static class AiHudRunForecast {
    /// <summary>Matches <c>BigDeck</c> bronze badge threshold.</summary>
    public const int OfficialBigDeckMin = 40;
    /// <summary>Matches <c>TinyDeck</c> bronze badge threshold.</summary>
    public const int OfficialSmallDeckMax = 20;

    public enum DeckStyle {
        Big,
        Small,
    }

    public sealed record DeckProfile(
        DeckStyle Style,
        int DeckSize,
        int TargetSize,
        float MeanValue,
        float ThinPreference,
        int ThinGap,
        bool IsExhaustFocused);

    public sealed record RunPrognosis(
        float WinRate,
        int RouteNodes,
        int PathRisk,
        float CombatsToRest,
        int NextFightScore,
        int NextFightIncoming,
        bool NextFightLethal);

    public static DeckProfile AnalyzeDeck(JsonObject snapshot) {
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);
        return new DeckProfile(
            InferStyle(metrics),
            metrics.DeckSize,
            OfficialSmallDeckMax,
            metrics.MeanValue,
            plan.ThinPreference,
            metrics.ThinGap,
            plan.IsExhaustFocused);
    }

    public static RunPrognosis AnalyzeRun(JsonObject snapshot, DeckProfile? profile = null) {
        profile ??= AnalyzeDeck(snapshot);
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);

        MapPlan? mapPlan = MapPathPlanner.CachedPlan;
        if (mapPlan == null && AiPlayServices.StateProvider.TryGetRunAndPlayer(out var state, out var player))
            mapPlan = MapPathPlanner.Plan(state, player, forceRefresh: false);

        int routeNodes = mapPlan?.PathCoords.Count ?? 0;
        int pathRisk = mapPlan?.PathRiskAtNext ?? 0;
        float combatsToRest = mapPlan?.CombatsToRestAtNext ?? 0f;

        int nextFightScore = NextFightDeckEvaluator.GetBaselineRouteScore(snapshot, plan);
        int incoming = 0;
        bool lethal = false;
        var route = NextFightRoute.ResolveFromSnapshot(snapshot);
        if (route.Count > 0) {
            incoming = route[0].IncomingTurn1;
            var turnOne = DeckDrawEvEstimator.EstimateAverage(snapshot, null, route[0]);
            lethal = turnOne.CanLethal;
        }

        float winRate = EstimateWinRate(snapshot, plan, metrics, profile, mapPlan, route.Count > 0 ? route[0] : null);

        return new RunPrognosis(
            winRate,
            routeNodes,
            pathRisk,
            combatsToRest,
            nextFightScore,
            incoming,
            lethal);
    }

    static DeckStyle InferStyle(DeckMetrics metrics) {
        if (metrics.DeckSize >= OfficialBigDeckMin)
            return DeckStyle.Big;

        if (metrics.DeckSize <= OfficialSmallDeckMax)
            return DeckStyle.Small;

        // Official badges only define <=20 and >=40; project the middle band toward the nearer threshold.
        int toBig = OfficialBigDeckMin - metrics.DeckSize;
        int toSmall = metrics.DeckSize - OfficialSmallDeckMax;
        return toBig <= toSmall ? DeckStyle.Big : DeckStyle.Small;
    }

    static float EstimateWinRate(
        JsonObject snapshot,
        DeckPlan plan,
        DeckMetrics metrics,
        DeckProfile profile,
        MapPlan? mapPlan,
        NextFightNode? nextFight) {
        var act = snapshot["actIndex"]?.GetValue<int>() ?? 0;
        var hp = IntentCalculator.HpRatio(snapshot);
        var quality = DeckEvaluator.DeckQualityScore(metrics, plan);

        float rate = act switch {
            0 => 0.58f,
            1 => 0.48f,
            _ => 0.38f,
        };

        rate += Math.Clamp((quality - 120) / 500f, -0.12f, 0.22f);
        rate += (hp - 0.55f) * 0.35f;
        rate -= Math.Clamp(metrics.SurvivalGap / 20f, 0f, 0.15f);

        if (profile.Style == DeckStyle.Big && profile.DeckSize >= OfficialBigDeckMin)
            rate -= 0.06f;
        else if (profile.Style == DeckStyle.Small && profile.DeckSize <= OfficialSmallDeckMax)
            rate += 0.03f;

        if (mapPlan != null)
            rate -= mapPlan.PathRiskAtNext / 250f;

        if (nextFight != null) {
            var turnOne = DeckDrawEvEstimator.EstimateAverage(snapshot, null, nextFight);
            if (turnOne.BlockGap <= 0)
                rate += 0.04f;
            else {
                var hpNow = snapshot["currentHp"]?.GetValue<int>() ?? 0;
                if (turnOne.BlockGap >= hpNow)
                    rate -= 0.12f;
                else
                    rate -= Math.Clamp(turnOne.BlockGap / 40f, 0f, 0.08f);
            }

            if (turnOne.CanLethal)
                rate += 0.03f;
        }

        return Math.Clamp(rate, 0.08f, 0.92f);
    }

    public static string StyleLabel(DeckStyle style) => style switch {
        DeckStyle.Big => I18N.T("ai.hud.deck.big", "Big deck"),
        _ => I18N.T("ai.hud.deck.small", "Small deck"),
    };

    public static bool IsBigDeck(DeckProfile profile) => profile.Style == DeckStyle.Big;

    public static bool MeetsOfficialBig(DeckProfile profile) =>
        profile.DeckSize >= OfficialBigDeckMin;

    public static bool MeetsOfficialSmall(DeckProfile profile) =>
        profile.DeckSize <= OfficialSmallDeckMax;
}
