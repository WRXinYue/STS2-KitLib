using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using KitLib.AI.Sts2.Snapshots;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.AI.Planning;

public sealed record MapPlan(
    IReadOnlyList<MapCoord> PathCoords,
    IReadOnlyList<(MapCoord From, MapCoord To)> Edges,
    int NextChildIndex,
    int PathScore,
    string Summary,
    MapCoord NextCoord,
    int PathRiskAtNext,
    float CombatsToRestAtNext,
    int ElitesToRestAtNext
);

public static class MapPathPlanner {
    static MapPlan? _cached;

    public static MapPlan? CachedPlan => _cached;

    public static void ClearCache() => _cached = null;

    public static MapPlan? Plan(RunState state, Player player, bool forceRefresh = false) {
        if (!forceRefresh && _cached != null)
            return _cached;

        var map = state.Map;
        if (map == null || map.GetRowCount() == 0)
            return null;

        var snapshot = GameSnapshot.Capture(state, player, Core.Schema.GamePhase.MapSelection);
        var ctx = MapRouteContext.FromSnapshot(snapshot);
        var survivalIndex = MapSurvivalIndex.Build(map);

        var points = map.GetAllMapPoints().OrderByDescending(p => p.coord.row).ToList();
        var bestToBoss = new Dictionary<MapCoord, int>();
        var bestChild = new Dictionary<MapCoord, MapCoord?>();

        foreach (var point in points) {
            if (point.Children.Count == 0) {
                bestToBoss[point.coord] = 0;
                bestChild[point.coord] = null;
                continue;
            }

            int baseNodeScore = MapNodeWeightScorer.ScoreNode(point.PointType, ctx);
            int best = int.MinValue;
            MapCoord? chosen = null;

            foreach (var child in point.Children) {
                if (!bestToBoss.TryGetValue(child.coord, out int childScore))
                    childScore = 0;

                int pathRisk = PathSurvivalRisk.Compute(
                    child.coord, survivalIndex, ctx.Metrics, ctx.HpRatio, ctx.Ascension);
                var segment = survivalIndex.GetOrDefault(child.coord);
                int total = baseNodeScore
                    + MapNodeWeightScorer.PathRiskNodeAdjust(
                        child.PointType, pathRisk, ctx, segment.ElitesToRest)
                    + MapNodeWeightScorer.EdgeBonus(point.PointType, child.PointType, ctx, pathRisk)
                    + childScore;
                if (total > best) {
                    best = total;
                    chosen = child.coord;
                }
            }

            bestToBoss[point.coord] = best == int.MinValue ? baseNodeScore : best;
            bestChild[point.coord] = chosen;
        }

        var available = GetAvailableCoords(state, map);
        if (available.Count == 0)
            return null;

        MapCoord nextCoord;
        int pathScore;
        List<MapCoord> path;

        if (state.VisitedMapCoords.Count == 0) {
            nextCoord = available
                .OrderByDescending(c => bestToBoss.GetValueOrDefault(c, int.MinValue))
                .ThenBy(c => c.col)
                .First();
            pathScore = bestToBoss.GetValueOrDefault(nextCoord, 0);
            path = BuildPathFrom(nextCoord, bestChild, map);
        }
        else {
            var current = state.VisitedMapCoords[^1];
            if (!bestChild.TryGetValue(current, out var firstHop) || firstHop == null) {
                nextCoord = available[0];
                pathScore = bestToBoss.GetValueOrDefault(nextCoord, 0);
                path = BuildPathFrom(nextCoord, bestChild, map);
            }
            else {
                nextCoord = firstHop.Value;
                pathScore = bestToBoss.GetValueOrDefault(current, 0);
                path = new List<MapCoord> { current };
                path.AddRange(BuildPathFrom(nextCoord, bestChild, map));
            }
        }

        int nextIdx = available.FindIndex(c => c.Equals(nextCoord));
        if (nextIdx < 0) nextIdx = 0;

        var edges = BuildEdges(path);
        var summary = BuildSummary(path, map);
        var nextSegment = survivalIndex.GetOrDefault(nextCoord);
        int pathRiskAtNext = PathSurvivalRisk.Compute(
            nextSegment.CombatsToRest,
            nextSegment.ElitesToRest,
            ctx.Metrics,
            ctx.HpRatio);

        _cached = new MapPlan(
            path,
            edges,
            nextIdx,
            pathScore,
            summary,
            nextCoord,
            pathRiskAtNext,
            nextSegment.CombatsToRest,
            nextSegment.ElitesToRest);
        return _cached;
    }

    public static MapPlan? PlanFromSnapshot(JsonObject snapshot, RunState state, Player player) =>
        Plan(state, player, forceRefresh: true);

    static List<MapCoord> GetAvailableCoords(RunState state, ActMap map) {
        var list = new List<MapCoord>();
        if (state.VisitedMapCoords.Count == 0) {
            foreach (var p in map.GetPointsInRow(0))
                list.Add(p.coord);
            return list;
        }

        var last = state.VisitedMapCoords[^1];
        var lastPoint = map.GetPoint(last);
        if (lastPoint == null) return list;

        foreach (var child in lastPoint.Children)
            list.Add(child.coord);
        return list;
    }

    static List<MapCoord> BuildPathFrom(MapCoord start, Dictionary<MapCoord, MapCoord?> bestChild, ActMap map) {
        var path = new List<MapCoord> { start };
        var current = start;
        var guard = 0;

        while (guard++ < map.GetRowCount() + 2) {
            if (!bestChild.TryGetValue(current, out var next) || next == null)
                break;
            path.Add(next.Value);
            current = next.Value;
        }

        return path;
    }

    static List<(MapCoord From, MapCoord To)> BuildEdges(IReadOnlyList<MapCoord> path) {
        var edges = new List<(MapCoord, MapCoord)>();
        for (int i = 0; i < path.Count - 1; i++)
            edges.Add((path[i], path[i + 1]));
        return edges;
    }

    static string BuildSummary(IReadOnlyList<MapCoord> path, ActMap map) {
        if (path.Count == 0) return "";

        var sb = new StringBuilder();
        for (int i = 0; i < path.Count; i++) {
            if (i > 0) sb.Append('→');
            var point = map.GetPoint(path[i]);
            sb.Append(ShortType(point?.PointType ?? MapPointType.Unassigned));
        }
        return sb.ToString();
    }

    static string ShortType(MapPointType type) => type switch {
        MapPointType.RestSite => "Rest",
        MapPointType.Shop => "Shop",
        MapPointType.Elite => "Elite",
        MapPointType.Monster => "M",
        MapPointType.Treasure => "Treasure",
        MapPointType.Unknown => "?",
        MapPointType.Boss => "Boss",
        MapPointType.Ancient => "Ancient",
        _ => type.ToString(),
    };
}
