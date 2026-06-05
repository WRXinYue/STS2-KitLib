using System;
using System.Text.Json.Nodes;
using DevMode.AI.Core.Schema;
using DevMode.AI.Planning;
using DevMode.AI.Sts2;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.AI.AutoPlay.Scoring;

public static class MapScorer {
    public static GameAction PickBest(JsonObject snapshot) {
        if (AiPlayServices.StateProvider.TryGetRunAndPlayer(out var state, out var player)) {
            var plan = MapPathPlanner.Plan(state, player) ?? MapPathPlanner.CachedPlan;
            if (plan != null) {
                var type = snapshot["mapNodes"]?[plan.NextChildIndex]?["pointType"]?.GetValue<string>()
                    ?? ShortTypeForCoord(state, plan.NextCoord);
                return new GameAction {
                    Type = ActionType.SelectMapNode,
                    TargetIndex = plan.NextChildIndex,
                    Reason = $"Map → {type} score={plan.PathScore} path={plan.Summary}",
                };
            }
        }

        return PickBestGreedy(snapshot);
    }

    static string ShortTypeForCoord(RunState state, MapCoord coord) {
        var point = state.Map?.GetPoint(coord);
        return point?.PointType.ToString() ?? "?";
    }

    static GameAction PickBestGreedy(JsonObject snapshot) {
        var nodes = snapshot["mapNodes"]?.AsArray();
        if (nodes == null || nodes.Count == 0) {
            return new GameAction {
                Type = ActionType.SelectMapNode,
                TargetIndex = 0,
                Reason = "First available node (no map data)",
            };
        }

        var ctx = MapRouteContext.FromSnapshot(snapshot);
        int bestIdx = 0;
        int bestScore = int.MinValue;

        for (int i = 0; i < nodes.Count; i++) {
            if (nodes[i] is not JsonObject node) continue;
            var typeStr = node["pointType"]?.GetValue<string>() ?? "";
            if (!Enum.TryParse<MapPointType>(typeStr, out var type))
                continue;

            int score = MapNodeWeightScorer.ScoreNode(type, ctx);
            if (score > bestScore) {
                bestScore = score;
                bestIdx = node["index"]?.GetValue<int>() ?? i;
            }
        }

        var label = nodes[bestIdx]?["pointType"]?.GetValue<string>() ?? "?";
        return new GameAction {
            Type = ActionType.SelectMapNode,
            TargetIndex = bestIdx,
            Reason = $"Map → {label} score={bestScore} (greedy fallback)",
        };
    }
}
