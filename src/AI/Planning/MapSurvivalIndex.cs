using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Map;

namespace KitLib.AI.Planning;

public sealed record MapSurvivalSegment(float CombatsToRest, int ElitesToRest);

/// <summary>Forward DP from each map coord: min combats / elites until the next rest site.</summary>
public sealed class MapSurvivalIndex {
    readonly Dictionary<MapCoord, MapSurvivalSegment> _segments = new();

    public static MapSurvivalIndex Build(ActMap map) {
        var index = new MapSurvivalIndex();
        index.Compute(map);
        return index;
    }

    public bool TryGet(MapCoord coord, out MapSurvivalSegment segment) =>
        _segments.TryGetValue(coord, out segment!);

    public MapSurvivalSegment GetOrDefault(MapCoord coord) =>
        _segments.GetValueOrDefault(coord, new MapSurvivalSegment(0f, 0));

    void Compute(ActMap map) {
        var points = map.GetAllMapPoints().OrderByDescending(p => p.coord.row).ToList();
        var combats = new Dictionary<MapCoord, float>();
        var elites = new Dictionary<MapCoord, int>();

        foreach (var point in points) {
            var coord = point.coord;

            if (point.PointType == MapPointType.RestSite) {
                combats[coord] = 0f;
                elites[coord] = 0;
                continue;
            }

            if (point.Children.Count == 0) {
                combats[coord] = CombatWeight(point.PointType);
                elites[coord] = point.PointType == MapPointType.Elite ? 1 : 0;
                continue;
            }

            float bestCombats = float.MaxValue;
            int bestElites = int.MaxValue;

            foreach (var child in point.Children) {
                if (!combats.TryGetValue(child.coord, out var childCombats))
                    continue;

                var totalCombats = CombatWeight(point.PointType) + childCombats;
                var totalElites = (point.PointType == MapPointType.Elite ? 1 : 0)
                    + elites.GetValueOrDefault(child.coord, 0);

                if (totalCombats < bestCombats
                    || (Math.Abs(totalCombats - bestCombats) < 0.001f && totalElites < bestElites)) {
                    bestCombats = totalCombats;
                    bestElites = totalElites;
                }
            }

            if (bestCombats >= float.MaxValue) {
                bestCombats = CombatWeight(point.PointType);
                bestElites = point.PointType == MapPointType.Elite ? 1 : 0;
            }

            combats[coord] = bestCombats;
            elites[coord] = bestElites;
        }

        foreach (var kv in combats)
            _segments[kv.Key] = new MapSurvivalSegment(kv.Value, elites.GetValueOrDefault(kv.Key, 0));
    }

    static float CombatWeight(MapPointType type) => type switch {
        MapPointType.Monster => 1f,
        MapPointType.Elite => 1f,
        MapPointType.Unknown => 0.7f,
        MapPointType.Ancient => 0.5f,
        MapPointType.Boss => 1f,
        _ => 0f,
    };
}
