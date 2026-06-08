using System;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using KitLib.Map;

namespace KitLib.Actions;

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
                MainFile.Logger.Warn("[KitLib] TryEnterRoom: no run in progress.");
                return false;
            }

            if (roomType == RoomType.Map)
                MapScreenUnlock.EnableFromDevPanel();

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
            MainFile.Logger.Warn($"[KitLib] TryEnterRoom({roomType}) failed: {ex.Message}");
            return false;
        }
    }
}
