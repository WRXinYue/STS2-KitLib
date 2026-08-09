using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Map;

namespace KitLib.AI.Planning;

/// <summary>Map route, rest EV, and per-node sim scores for Dev Viewer.</summary>
public static class MapRouteInsights {
    public static bool HasData(JsonObject snapshot) {
        var route = NextFightRoute.ResolveFromSnapshot(snapshot);
        var mapNodes = snapshot["mapNodes"]?.AsArray();
        var restOptions = snapshot["restOptions"]?.AsArray();
        return route.Count > 0
            || (mapNodes != null && mapNodes.Count > 0)
            || (restOptions != null && restOptions.Count > 0);
    }

    public static MapPlan? ResolvePlan() => MapPathPlanner.CachedPlan;

    public static RestEvBreakdown EvaluateRest(JsonObject snapshot) =>
        RestEvScorer.Evaluate(snapshot);

    public static List<RouteFightEvInsight> BuildRouteFightEv(JsonObject snapshot, DeckPlan plan) {
        var route = NextFightRoute.ResolveFromSnapshot(snapshot);
        var list = new List<RouteFightEvInsight>(route.Count);

        foreach (var fight in route) {
            int reward = EliteEvEstimator.EstimateRewardEv(snapshot, plan, fight.RoomType);
            int cost = EliteEvEstimator.EstimateFightCost(snapshot, fight);
            list.Add(new RouteFightEvInsight(
                fight.EncounterId,
                fight.RoomType.ToString(),
                fight.Weight,
                reward,
                cost,
                reward - cost,
                fight.IncomingTurn1));
        }

        return list;
    }

    public static List<MapOptionInsight> BuildMapOptions(JsonObject snapshot, DeckPlan plan) {
        var nodes = snapshot["mapNodes"]?.AsArray();
        if (nodes == null || nodes.Count == 0)
            return [];

        var ctx = MapRouteContext.FromSnapshot(snapshot);
        var list = new List<MapOptionInsight>(nodes.Count);

        foreach (var node in nodes) {
            if (node is not JsonObject obj)
                continue;

            var typeStr = obj["pointType"]?.GetValue<string>() ?? "";
            if (!Enum.TryParse<MapPointType>(typeStr, out var type))
                continue;

            int row = obj["row"]?.GetValue<int>() ?? 0;
            int col = obj["col"]?.GetValue<int>() ?? 0;
            // Viewer only: heuristic scores — live encounter build is too heavy per poll.
            int score = MapNodeEvScorer.ScoreNode(type, ctx, snapshot, combatFight: null, forRouteDp: true);
            list.Add(new MapOptionInsight(
                obj["index"]?.GetValue<int>() ?? list.Count,
                typeStr,
                score,
                row,
                col));
        }

        list.Sort((a, b) => b.Score.CompareTo(a.Score));
        return list;
    }

    public sealed record RouteFightEvInsight(
        string EncounterId,
        string RoomType,
        float Weight,
        int RewardEv,
        int FightCost,
        int NetEv,
        int IncomingTurn1);

    public sealed record MapOptionInsight(
        int Index,
        string PointType,
        int Score,
        int Row,
        int Col);
}
