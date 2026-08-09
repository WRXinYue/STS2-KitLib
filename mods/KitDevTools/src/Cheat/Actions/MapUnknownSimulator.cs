using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace KitLib.Actions;

/// <summary>
/// Read-only simulation of map travel along a shortest path, mirroring
/// <see cref="RunManager.RollRoomTypeFor"/> / <see cref="UnknownMapPointOdds.Roll"/> and
/// <see cref="ActModel.PullNextEvent"/>.
/// </summary>
internal static class MapUnknownSimulator {
    private static readonly FieldInfo? RoomsField =
        typeof(ActModel).GetField("_rooms", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? BaseOddsField =
        typeof(UnknownMapPointOdds).GetField("_baseOdds", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? NonEventOddsField =
        typeof(UnknownMapPointOdds).GetField("_nonEventOdds", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? RngField =
        typeof(UnknownMapPointOdds).GetField("_rng", BindingFlags.NonPublic | BindingFlags.Instance);

    internal sealed record Prediction(
        RoomType RoomType,
        EncounterModel? Encounter,
        EventModel? Event,
        bool IsApproximate);

    internal static Prediction? PredictUnknownNode(RunState state, MapPoint target) {
        var path = FindShortestPath(state, target);
        if (path == null || path.Count == 0)
            return null;

        bool multiPath = HasMultipleEqualShortPaths(state, target, path.Count);
        var sim = SimulationState.Create(state);
        if (sim == null)
            return null;

        MapPoint? fromNode = state.CurrentMapCoord.HasValue
            ? state.Map?.GetPoint(state.CurrentMapCoord.Value)
            : null;

        Prediction? result = null;
        foreach (var node in path) {
            var roomType = ResolveRoomType(state, sim, node, fromNode);
            if (node.coord.Equals(target.coord)) {
                result = BuildPrediction(state, sim, roomType);
                break;
            }

            ApplyVisit(state, sim, node.PointType, roomType);
            fromNode = node;
        }

        if (result == null)
            return null;

        return result with { IsApproximate = result.IsApproximate || multiPath };
    }

    static Prediction BuildPrediction(RunState state, SimulationState sim, RoomType roomType) {
        return roomType switch {
            RoomType.Event => new Prediction(
                roomType,
                Encounter: null,
                PeekNextEvent(state, sim),
                IsApproximate: false),
            RoomType.Monster or RoomType.Elite => new Prediction(
                roomType,
                PeekNextEncounter(state, sim, roomType),
                Event: null,
                IsApproximate: false),
            _ => new Prediction(roomType, null, null, false),
        };
    }

    static RoomType ResolveRoomType(RunState state, SimulationState sim, MapPoint node, MapPoint? fromNode) {
        if (node.PointType != MapPointType.Unknown)
            return MapPointTypeToRoomType(node.PointType);

        var children = fromNode?.Children
            ?? (state.CurrentMapCoord.HasValue
                ? state.Map?.GetPoint(state.CurrentMapCoord.Value)?.Children
                : null)
            ?? [];
        var blacklist = BuildBlacklist(sim.LastRoomWasShop, children);
        return RollUnknown(sim, state, blacklist);
    }

    static void ApplyVisit(RunState state, SimulationState sim, MapPointType pointType, RoomType roomType) {
        if (roomType == RoomType.Shop)
            sim.LastRoomWasShop = true;
        else if (roomType is RoomType.Treasure or RoomType.RestSite or RoomType.Event)
            sim.LastRoomWasShop = false;

        if (roomType == RoomType.Event)
            ConsumeNextEvent(state, sim);
        else if (roomType == RoomType.Monster)
            sim.NormalEncountersVisited++;
        else if (roomType == RoomType.Elite)
            sim.EliteEncountersVisited++;
        else if (roomType == RoomType.Boss)
            sim.BossEncountersVisited++;

        if (pointType == MapPointType.Unknown)
            sim.SimulatedUnknownHistoryCount++;
    }

    /// <summary>Matches <see cref="RunManager.BuildRoomTypeBlacklist"/>.</summary>
    static HashSet<RoomType> BuildBlacklist(bool lastWasShop, IReadOnlyCollection<MapPoint> children) {
        var blacklist = new HashSet<RoomType>();
        if (lastWasShop || (children.Count > 0 && children.All(p => p.PointType == MapPointType.Shop)))
            blacklist.Add(RoomType.Shop);
        return blacklist;
    }

    /// <summary>Clone of <see cref="UnknownMapPointOdds.Roll"/> using forked RNG + odds snapshot.</summary>
    static RoomType RollUnknown(SimulationState sim, RunState state, HashSet<RoomType> blacklist) {
        int unknownHistoryCount = state.MapPointHistory
            .SelectMany(l => l)
            .Count(p => p.MapPointType == MapPointType.Unknown);
        int effectiveUnknown = unknownHistoryCount + sim.SimulatedUnknownHistoryCount;

        if (state.UnlockState.NumberOfRuns == 0) {
            if (effectiveUnknown < 2)
                return RoomType.Event;
            if (effectiveUnknown == 2)
                return RoomType.Monster;
        }

        var odds = sim.Odds;
        var nonEventOdds = (Dictionary<RoomType, float>)NonEventOddsField!.GetValue(odds)!;
        var baseOdds = (Dictionary<RoomType, float>)BaseOddsField!.GetValue(odds)!;
        var rng = (Rng)RngField!.GetValue(odds)!;

        IReadOnlySet<RoomType> roomTypes = nonEventOdds.Keys
            .Append(RoomType.Event)
            .Except(blacklist)
            .ToHashSet();
        roomTypes = Hook.ModifyUnknownMapPointRoomTypes(state, roomTypes);
        RoomType roomType = roomTypes.Contains(RoomType.Event)
            ? RoomType.Event
            : roomTypes.Order().First();

        float roll = rng.NextFloat();
        float cumulative = 0f;
        foreach (var (roomTypeKey, weight) in nonEventOdds) {
            if (!roomTypes.Contains(roomTypeKey) || weight < 0f)
                continue;
            cumulative += weight;
            if (roll <= cumulative) {
                roomType = roomTypeKey;
                break;
            }
        }

        foreach (var (roomTypeKey, baseWeight) in baseOdds) {
            if (roomType == roomTypeKey)
                nonEventOdds[roomTypeKey] = baseWeight;
            else if (roomTypes.Contains(roomTypeKey)) {
                float increase = Hook.ModifyOddsIncreaseForUnrolledRoomType(state, roomTypeKey, baseWeight);
                nonEventOdds[roomTypeKey] += increase;
            }
        }

        return roomType;
    }

    /// <summary>Matches <see cref="ActModel.PullNextEvent"/> without mutating <see cref="RunState"/>.</summary>
    static EventModel? ConsumeNextEvent(RunState state, SimulationState sim) {
        var ev = PeekNextEvent(state, sim);
        if (ev == null)
            return null;

        sim.VisitedEventIds.Add(ev.Id);
        sim.EventsVisited++;
        return ev;
    }

    /// <summary>Matches <see cref="RoomSet.EnsureNextEventIsValid"/> + <see cref="Hook.ModifyNextEvent"/>.</summary>
    static EventModel? PeekNextEvent(RunState state, SimulationState sim) {
        var roomSet = GetRoomSet(state.Act);
        if (roomSet == null || roomSet.events.Count == 0)
            return null;

        int eventsVisited = sim.EventsVisited;
        for (int i = 0; i < roomSet.events.Count; i++) {
            var ev = roomSet.events[eventsVisited % roomSet.events.Count];
            if (ev.IsAllowed(state) && !sim.VisitedEventIds.Contains(ev.Id))
                return Hook.ModifyNextEvent(state, ev);
            eventsVisited++;
        }

        return Hook.ModifyNextEvent(
            state,
            roomSet.events[eventsVisited % roomSet.events.Count]);
    }

    static EncounterModel? PeekNextEncounter(RunState state, SimulationState sim, RoomType roomType) {
        var act = state.Act;
        if (act == null)
            return null;
        if (roomType == RoomType.Boss)
            return act.BossEncounter;

        var roomSet = GetRoomSet(act);
        if (roomSet == null)
            return null;

        if (roomType == RoomType.Monster && roomSet.normalEncounters.Count > 0)
            return roomSet.normalEncounters[
                sim.NormalEncountersVisited % roomSet.normalEncounters.Count];

        if (roomType == RoomType.Elite && roomSet.eliteEncounters.Count > 0)
            return roomSet.eliteEncounters[
                sim.EliteEncountersVisited % roomSet.eliteEncounters.Count];

        return null;
    }

    static RoomSet? GetRoomSet(ActModel? act) =>
        act == null ? null : RoomsField?.GetValue(act) as RoomSet;

    static RoomType MapPointTypeToRoomType(MapPointType pointType) => pointType switch {
        MapPointType.Monster => RoomType.Monster,
        MapPointType.Elite => RoomType.Elite,
        MapPointType.Boss => RoomType.Boss,
        MapPointType.Shop => RoomType.Shop,
        MapPointType.Treasure => RoomType.Treasure,
        MapPointType.RestSite => RoomType.RestSite,
        MapPointType.Ancient => RoomType.Event,
        _ => RoomType.Unassigned,
    };

    static List<MapPoint>? FindShortestPath(RunState state, MapPoint target) {
        var map = state.Map;
        if (map == null)
            return null;

        IEnumerable<MapPoint> starts;
        if (state.CurrentMapCoord.HasValue) {
            var cur = map.GetPoint(state.CurrentMapCoord.Value);
            if (cur == null)
                return null;
            if (cur.coord.Equals(target.coord))
                return [target];
            starts = cur.Children;
        }
        else {
            starts = map.GetAllMapPoints().Where(p => p.parents.Count == 0);
        }

        var dist = new Dictionary<MapCoord, int>();
        var parent = new Dictionary<MapCoord, MapCoord>();
        var queue = new Queue<MapPoint>();

        foreach (var s in starts) {
            if (!dist.ContainsKey(s.coord)) {
                dist[s.coord] = 1;
                parent[s.coord] = s.coord;
                queue.Enqueue(s);
            }
        }

        while (queue.Count > 0) {
            var p = queue.Dequeue();
            if (p.coord.Equals(target.coord))
                break;

            foreach (var child in p.Children) {
                if (dist.ContainsKey(child.coord))
                    continue;
                dist[child.coord] = dist[p.coord] + 1;
                parent[child.coord] = p.coord;
                queue.Enqueue(child);
            }
        }

        if (!dist.ContainsKey(target.coord))
            return null;

        var path = new List<MapPoint>();
        var coord = target.coord;
        while (!parent[coord].Equals(coord)) {
            path.Add(map.GetPoint(coord)!);
            coord = parent[coord];
        }

        path.Reverse();
        return path;
    }

    static bool HasMultipleEqualShortPaths(RunState state, MapPoint target, int shortestLen) {
        var map = state.Map;
        if (map == null)
            return false;

        int CountPaths(MapPoint node, int depth) {
            if (node.coord.Equals(target.coord))
                return depth == shortestLen ? 1 : 0;
            if (depth >= shortestLen)
                return 0;

            int sum = 0;
            foreach (var child in node.Children)
                sum += CountPaths(child, depth + 1);
            return sum;
        }

        IEnumerable<MapPoint> starts;
        if (state.CurrentMapCoord.HasValue) {
            var cur = map.GetPoint(state.CurrentMapCoord.Value);
            starts = cur?.Children ?? [];
        }
        else {
            starts = map.GetAllMapPoints().Where(p => p.parents.Count == 0);
        }

        int total = starts.Sum(s => CountPaths(s, 1));
        return total > 1;
    }

    sealed class SimulationState {
        public required UnknownMapPointOdds Odds { get; init; }
        public int NormalEncountersVisited { get; set; }
        public int EliteEncountersVisited { get; set; }
        public int EventsVisited { get; set; }
        public int BossEncountersVisited { get; set; }
        public HashSet<ModelId> VisitedEventIds { get; } = [];
        public bool LastRoomWasShop { get; set; }
        public int SimulatedUnknownHistoryCount { get; set; }

        public static SimulationState? Create(RunState state) {
            var roomSet = GetRoomSet(state.Act);
            if (roomSet == null)
                return null;

            var oddsSave = state.Odds.ToSerializable();
            var rng = new Rng(state.Rng.Seed, "unknown_map_point");
            var odds = new UnknownMapPointOdds(rng) {
                MonsterOdds = oddsSave.UnknownMapPointMonsterOddsValue,
                EliteOdds = oddsSave.UnknownMapPointEliteOddsValue,
                TreasureOdds = oddsSave.UnknownMapPointTreasureOddsValue,
                ShopOdds = oddsSave.UnknownMapPointShopOddsValue,
            };

            bool lastShop = state.CurrentMapPointHistoryEntry?.HasRoomOfType(RoomType.Shop) ?? false;

            var sim = new SimulationState {
                Odds = odds,
                NormalEncountersVisited = roomSet.normalEncountersVisited,
                EliteEncountersVisited = roomSet.eliteEncountersVisited,
                EventsVisited = roomSet.eventsVisited,
                BossEncountersVisited = roomSet.bossEncountersVisited,
                LastRoomWasShop = lastShop,
            };
            sim.VisitedEventIds.UnionWith(state.VisitedEventIds);
            return sim;
        }
    }
}
