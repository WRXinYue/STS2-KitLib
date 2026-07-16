using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KitLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions;

/// <summary>Predicts map node contents (encounters, events, room types) including dev overrides.</summary>
internal static class MapEncounterPreview {
    private static readonly FieldInfo? RoomsField =
        typeof(ActModel).GetField("_rooms", BindingFlags.NonPublic | BindingFlags.Instance);

    internal sealed record Preview(
        MapPoint Point,
        int Floor,
        RoomType CombatRoomType,
        EncounterModel? Encounter,
        EventModel? Event,
        bool IsOverride,
        bool IsFloorOverride,
        bool IsGlobalOrTypeOverride,
        bool IsCurrentRoom,
        bool IsCombatNode,
        bool IsApproximate);

    internal static bool IsCombatNode(MapPointType pointType) =>
        pointType is MapPointType.Monster or MapPointType.Elite or MapPointType.Boss;

    internal static bool IsPreviewableNode(MapPointType pointType) =>
        pointType is MapPointType.Monster or MapPointType.Elite or MapPointType.Boss
            or MapPointType.Ancient or MapPointType.Shop or MapPointType.Treasure
            or MapPointType.RestSite or MapPointType.Unknown;

    internal static RoomType? ToRoomType(MapPointType pointType) => pointType switch {
        MapPointType.Monster => RoomType.Monster,
        MapPointType.Elite => RoomType.Elite,
        MapPointType.Boss => RoomType.Boss,
        MapPointType.Shop => RoomType.Shop,
        MapPointType.Treasure => RoomType.Treasure,
        MapPointType.RestSite => RoomType.RestSite,
        MapPointType.Ancient => RoomType.Event,
        _ => null,
    };

    internal static Preview? Build(RunState state, MapPoint point) {
        if (IsCombatNode(point.PointType))
            return BuildCombat(state, point);

        int floor = point.coord.row + 1;
        bool isCurrentRoom = state.CurrentMapCoord.HasValue
            && point.coord.Equals(state.CurrentMapCoord.Value);

        return point.PointType switch {
            MapPointType.Ancient => BuildAncient(state, point, floor, isCurrentRoom),
            MapPointType.Shop => BuildFixedRoom(point, floor, isCurrentRoom, RoomType.Shop),
            MapPointType.Treasure => BuildFixedRoom(point, floor, isCurrentRoom, RoomType.Treasure),
            MapPointType.RestSite => BuildFixedRoom(point, floor, isCurrentRoom, RoomType.RestSite),
            MapPointType.Unknown => BuildUnknown(state, point, floor, isCurrentRoom),
            _ => null,
        };
    }

    static Preview BuildCombat(RunState state, MapPoint point) {
        var roomType = ToRoomType(point.PointType)!.Value;

        int floor = point.coord.row + 1;
        bool isCurrentRoom = state.CurrentMapCoord.HasValue
            && point.coord.Equals(state.CurrentMapCoord.Value);

        EncounterModel? encounter;
        bool isFloorOverride = KitLibState.FloorOverrides.ContainsKey(floor);
        bool isGlobalOrTypeOverride = false;
        bool isOverride;

        if (isCurrentRoom) {
            encounter = (state.CurrentRoom as CombatRoom)?.Encounter;
            isOverride = isFloorOverride
                || KitLibState.ResolveOverride(roomType, floor) != null;
        }
        else {
            var floorEnc = KitLibState.FloorOverrides.TryGetValue(floor, out var fo) ? fo : null;
            var modeEnc = KitLibState.EnemyMode switch {
                EnemyMode.Global => KitLibState.GlobalEncounterOverride,
                EnemyMode.PerType => KitLibState.RoomTypeOverrides.TryGetValue(roomType, out var enc)
                    ? enc
                    : null,
                _ => null,
            };
            isGlobalOrTypeOverride = modeEnc != null && floorEnc == null;
            var overrideEnc = KitLibState.ResolveOverride(roomType, floor);
            encounter = overrideEnc ?? PredictEncounter(state, point, roomType);
            isOverride = overrideEnc != null;
        }

        return new Preview(
            point,
            floor,
            roomType,
            encounter,
            Event: null,
            isOverride,
            isFloorOverride,
            isGlobalOrTypeOverride,
            isCurrentRoom,
            IsCombatNode: true,
            IsApproximate: false);
    }

    static Preview BuildAncient(RunState state, MapPoint point, int floor, bool isCurrentRoom) {
        EventModel? ancient = TryGetAncient(state);
        return new Preview(
            point,
            floor,
            RoomType.Event,
            Encounter: null,
            ancient,
            IsOverride: false,
            IsFloorOverride: false,
            IsGlobalOrTypeOverride: false,
            isCurrentRoom,
            IsCombatNode: false,
            IsApproximate: false);
    }

    static Preview BuildFixedRoom(MapPoint point, int floor, bool isCurrentRoom, RoomType roomType) =>
        new(
            point,
            floor,
            roomType,
            Encounter: null,
            Event: null,
            IsOverride: false,
            IsFloorOverride: false,
            IsGlobalOrTypeOverride: false,
            isCurrentRoom,
            IsCombatNode: false,
            IsApproximate: false);

    static Preview? BuildUnknown(RunState state, MapPoint point, int floor, bool isCurrentRoom) {
        var predicted = MapUnknownSimulator.PredictUnknownNode(state, point);
        if (predicted == null) {
            return new Preview(
                point,
                floor,
                RoomType.Unassigned,
                Encounter: null,
                Event: null,
                IsOverride: false,
                IsFloorOverride: false,
                IsGlobalOrTypeOverride: false,
                isCurrentRoom,
                IsCombatNode: false,
                IsApproximate: true);
        }

        bool isCombat = predicted.RoomType is RoomType.Monster or RoomType.Elite or RoomType.Boss;
        return new Preview(
            point,
            floor,
            predicted.RoomType,
            predicted.Encounter,
            predicted.Event,
            IsOverride: false,
            IsFloorOverride: false,
            IsGlobalOrTypeOverride: false,
            isCurrentRoom,
            IsCombatNode: isCombat,
            IsApproximate: predicted.IsApproximate);
    }

    static AncientEventModel? TryGetAncient(RunState state) {
        try {
            var act = state.Act;
            if (act == null)
                return null;
            var roomSet = RoomsField?.GetValue(act) as RoomSet;
            return roomSet is { HasAncient: true } ? roomSet.Ancient : null;
        }
        catch {
            return null;
        }
    }

    internal static EncounterModel? PredictEncounter(RunState state, MapPoint targetPoint, RoomType roomType) {
        try {
            var act = state.Act;
            if (act == null) return null;

            if (roomType == RoomType.Boss)
                return act.BossEncounter;

            var roomSet = RoomsField?.GetValue(act) as RoomSet;
            if (roomSet == null)
                return act.PullNextEncounter(roomType);

            int offset = CountRoomsOnPath(state, targetPoint, roomType switch {
                RoomType.Monster => MapPointType.Monster,
                RoomType.Elite => MapPointType.Elite,
                _ => MapPointType.Unassigned,
            }) ?? 0;

            if (roomType == RoomType.Monster && roomSet.normalEncounters.Count > 0)
                return roomSet.normalEncounters[
                    (roomSet.normalEncountersVisited + offset) % roomSet.normalEncounters.Count];

            if (roomType == RoomType.Elite && roomSet.eliteEncounters.Count > 0)
                return roomSet.eliteEncounters[
                    (roomSet.eliteEncountersVisited + offset) % roomSet.eliteEncounters.Count];
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"MapEncounterPreview: Failed to predict encounter: {ex.Message}");
        }

        return null;
    }

    static int? CountRoomsOnPath(RunState state, MapPoint target, MapPointType pointType) {
        try {
            var map = state.Map;
            if (map == null) return null;

            IEnumerable<MapPoint> startPoints;
            if (state.CurrentMapCoord.HasValue) {
                var cur = map.GetPoint(state.CurrentMapCoord.Value);
                startPoints = cur?.Children ?? Enumerable.Empty<MapPoint>();
            }
            else {
                startPoints = map.GetAllMapPoints().Where(p => p.parents.Count == 0);
            }

            var minOffset = new Dictionary<MapCoord, int>();
            var queue = new Queue<(MapPoint point, int offset)>();

            foreach (var sp in startPoints) {
                if (!minOffset.ContainsKey(sp.coord)) {
                    minOffset[sp.coord] = 0;
                    queue.Enqueue((sp, 0));
                }
            }

            while (queue.Count > 0) {
                var (p, offset) = queue.Dequeue();

                if (minOffset.TryGetValue(p.coord, out int best) && offset > best)
                    continue;

                if (p.coord.Equals(target.coord))
                    return offset;

                bool countsHere = p.PointType == pointType;
                int costThrough = offset + (countsHere ? 1 : 0);

                foreach (var child in p.Children) {
                    if (!minOffset.TryGetValue(child.coord, out int childBest) || costThrough < childBest) {
                        minOffset[child.coord] = costThrough;
                        queue.Enqueue((child, costThrough));
                    }
                }
            }
        }
        catch {
            // ignore
        }

        return null;
    }
}
