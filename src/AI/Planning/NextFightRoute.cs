using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Actions;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Sts2;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.AI.Planning;

public sealed record NextFightNode(
    float Weight,
    RoomType RoomType,
    string EncounterId,
    IReadOnlyList<CombatEnemy> Enemies,
    int IncomingTurn1);

/// <summary>Weighted upcoming combat nodes on the planned map route.</summary>
public static class NextFightRoute {
    public const int MaxCombats = 3;

    static readonly float[] DistanceWeights = [1.0f, 0.55f, 0.30f];

    public static IReadOnlyList<NextFightNode> Resolve(RunState state, Player player) {
        var plan = MapPathPlanner.Plan(state, player, forceRefresh: true);
        if (plan == null || plan.PathCoords.Count == 0)
            return [];

        var map = state.Map;
        if (map == null)
            return [];

        int startIdx = ResolveStartIndex(state, plan.PathCoords);
        var nodes = new List<NextFightNode>();
        int combatIdx = 0;

        for (int i = startIdx; i < plan.PathCoords.Count && combatIdx < MaxCombats; i++) {
            var point = map.GetPoint(plan.PathCoords[i]);
            if (point == null)
                continue;

            var roomType = MapEncounterPreview.ToRoomType(point.PointType);
            if (roomType == null)
                continue;

            var preview = MapEncounterPreview.Build(state, point);
            if (preview?.Encounter == null)
                continue;

            var encounterId = ((AbstractModel)preview.Encounter).Id.Entry ?? "?";
            var enemies = EncounterCombatFactory.CreateEnemies(
                preview.Encounter,
                preview.CombatRoomType,
                state.CurrentActIndex);
            if (enemies.Count == 0)
                continue;

            int incoming = enemies.Where(e => e.IsAlive).Sum(e => e.IntentDamage);
            nodes.Add(new NextFightNode(
                DistanceWeights[combatIdx],
                preview.CombatRoomType,
                encounterId,
                enemies,
                incoming));
            combatIdx++;
        }

        return nodes;
    }

    public static IReadOnlyList<NextFightNode> ResolveFromSnapshot(System.Text.Json.Nodes.JsonObject snapshot) {
        // Autoplay scoring runs off the main thread — snapshot preview only (no live map / Godot).
        return ParsePreview(snapshot);
    }

    static int ResolveStartIndex(RunState state, IReadOnlyList<MapCoord> path) {
        if (state.VisitedMapCoords.Count == 0)
            return 0;

        var current = state.VisitedMapCoords[^1];
        for (int i = 0; i < path.Count; i++) {
            if (path[i].Equals(current))
                return Math.Min(i + 1, path.Count - 1);
        }

        return path.Count > 1 ? 1 : 0;
    }

    static IReadOnlyList<NextFightNode> ParsePreview(System.Text.Json.Nodes.JsonObject snapshot) {
        var arr = snapshot["nextFightPreview"]?.AsArray();
        if (arr == null || arr.Count == 0)
            return [];

        var nodes = new List<NextFightNode>();
        foreach (var node in arr) {
            if (node is not System.Text.Json.Nodes.JsonObject obj)
                continue;

            var roomRaw = obj["roomType"]?.GetValue<string>() ?? "";
            if (!Enum.TryParse<RoomType>(roomRaw, out var roomType))
                continue;

            var enemies = EncounterCombatFactory.CreateEnemiesFromPreview(obj["enemies"]?.AsArray());
            if (enemies.Count == 0)
                continue;

            nodes.Add(new NextFightNode(
                obj["weight"]?.GetValue<float>() ?? 1f,
                roomType,
                obj["encounterId"]?.GetValue<string>() ?? "?",
                enemies,
                obj["incomingTurn1"]?.GetValue<int>() ?? 0));
        }

        return nodes;
    }
}
