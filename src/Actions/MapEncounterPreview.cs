using KitLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions;

/// <summary>Predicts which encounter a map combat node will use, including dev overrides.</summary>
internal static class MapEncounterPreview {
    private static readonly FieldInfo? RoomsField =
        typeof(ActModel).GetField("_rooms", BindingFlags.NonPublic | BindingFlags.Instance);

    internal sealed record Preview(
        MapPoint Point,
        int Floor,
        RoomType CombatRoomType,
        EncounterModel? Encounter,
        bool IsOverride,
        bool IsFloorOverride,
        bool IsGlobalOrTypeOverride,
        bool IsCurrentRoom,
        bool IsCombatNode);

    internal static bool IsCombatNode(MapPointType pointType) =>
        pointType is MapPointType.Monster or MapPointType.Elite or MapPointType.Boss;

    internal static RoomType? ToRoomType(MapPointType pointType) => pointType switch {
        MapPointType.Monster => RoomType.Monster,
        MapPointType.Elite => RoomType.Elite,
        MapPointType.Boss => RoomType.Boss,
        _ => null,
    };

    internal static Preview? Build(RunState state, MapPoint point) {
        var roomType = ToRoomType(point.PointType);
        if (roomType == null)
            return null;

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
                || KitLibState.ResolveOverride(roomType.Value, floor) != null;
        }
        else {
            var floorEnc = KitLibState.FloorOverrides.TryGetValue(floor, out var fo) ? fo : null;
            var modeEnc = KitLibState.EnemyMode switch {
                EnemyMode.Global => KitLibState.GlobalEncounterOverride,
                EnemyMode.PerType => KitLibState.RoomTypeOverrides.TryGetValue(roomType.Value, out var enc)
                    ? enc
                    : null,
                _ => null,
            };
            isGlobalOrTypeOverride = modeEnc != null && floorEnc == null;
            var overrideEnc = KitLibState.ResolveOverride(roomType.Value, floor);
            encounter = overrideEnc ?? PredictEncounter(state, point, roomType.Value);
            isOverride = overrideEnc != null;
        }

        return new Preview(
            point,
            floor,
            roomType.Value,
            encounter,
            isOverride,
            isFloorOverride,
            isGlobalOrTypeOverride,
            isCurrentRoom,
            IsCombatNode: true);
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

            int offset = CountSameTypeRoomsOnPath(state, targetPoint, roomType) ?? 0;

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

    private static int? CountSameTypeRoomsOnPath(RunState state, MapPoint target, RoomType roomType) {
        var targetType = roomType == RoomType.Monster ? MapPointType.Monster : MapPointType.Elite;

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

                int costThrough = offset + (p.PointType == targetType ? 1 : 0);

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
