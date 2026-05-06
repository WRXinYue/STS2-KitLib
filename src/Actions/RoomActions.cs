using System;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Actions;

internal static class RoomActions {
    public static bool IsRunInProgress => RunManager.Instance?.IsInProgress == true;

    /// <summary>
    /// Teleport directly into the given room type using the game's debug room entry API.
    /// Requires an active run; silently fails (with a log warning) otherwise.
    /// </summary>
    public static bool TryEnterRoom(RoomType roomType) {
        try {
            var rm = RunManager.Instance;
            if (rm == null || !rm.IsInProgress) {
                MainFile.Logger.Warn("[DevMode] TryEnterRoom: no run in progress.");
                return false;
            }

            if (roomType == RoomType.Map && !DevModeState.MapCheats.FreeTravelFromDevRoomMap) {
                DevModeState.MapCheats.FreeTravelFromDevRoomMap = true;
            }

            MapPointType pointType = roomType switch {
                RoomType.Shop => MapPointType.Shop,
                RoomType.RestSite => MapPointType.RestSite,
                RoomType.Treasure => MapPointType.Treasure,
                RoomType.Monster => MapPointType.Monster,
                RoomType.Elite => MapPointType.Elite,
                RoomType.Boss => MapPointType.Boss,
                _ => MapPointType.Unassigned,
            };

            TaskHelper.RunSafely(rm.EnterRoomDebug(roomType, pointType));
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[DevMode] TryEnterRoom({roomType}) failed: {ex.Message}");
            return false;
        }
    }

    public static bool TryEnterMapPoint(MapCoord coord, MapPointType pointType) {
        var roomType = pointType switch {
            MapPointType.Shop => RoomType.Shop,
            MapPointType.RestSite => RoomType.RestSite,
            MapPointType.Treasure => RoomType.Treasure,
            MapPointType.Monster => RoomType.Monster,
            MapPointType.Elite => RoomType.Elite,
            MapPointType.Boss => RoomType.Boss,
            MapPointType.Unknown => RoomType.Event,
            MapPointType.Ancient => RoomType.Event,
            _ => RoomType.Unassigned
        };

        if (roomType == RoomType.Unassigned) {
            MainFile.Logger.Warn($"[DevMode] TryEnterMapPoint rejected: unsupported point type {pointType}");
            return false;
        }

        try {
            var rm = RunManager.Instance;
            if (rm == null || !rm.IsInProgress) {
                MainFile.Logger.Warn("[DevMode] TryEnterMapPoint: no run in progress.");
                return false;
            }

            // Use map-coord debug entry to keep map progression/visited coord in sync.
            TaskHelper.RunSafely(rm.EnterMapCoordDebug(coord, roomType, pointType));
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[DevMode] TryEnterMapPoint({coord}, {pointType}) failed: {ex.Message}");
            return false;
        }
    }
}
