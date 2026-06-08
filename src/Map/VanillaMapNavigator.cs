using System;
using System.Linq;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Map;

/// <summary>
/// Jump to a map coordinate via vanilla <see cref="RunManager.EnterMapPointInternal"/>.
/// Do not use <see cref="RunManager.EnterMapCoordDebug"/> — it passes MapPointType.Unassigned to CreateRoom
/// and turns Ancient nodes into random events.
/// </summary>
internal static class VanillaMapNavigator {
    public static bool TryGoTo(MapCoord coord) {
        try {
            var rm = RunManager.Instance;
            if (rm == null || !rm.IsInProgress) {
                MainFile.Logger.Warn("[KitLib.MapJump] TryGoTo: no run in progress");
                return false;
            }

            var state = rm.DebugOnlyGetState();
            if (state?.Map == null) {
                MainFile.Logger.Warn("[KitLib.MapJump] TryGoTo: no map on run state");
                return false;
            }

            var point = state.Map.GetPoint(coord);
            if (point == null) {
                MainFile.Logger.Warn($"[KitLib.MapJump] TryGoTo: no map point at {coord}");
                return false;
            }

            MapPointType actualType = point.PointType;

            if (!state.VisitedMapCoords.Contains(coord))
                state.AddVisitedMapCoord(coord);

            MainFile.Logger.Info(
                $"[KitLib.MapJump] EnterMapPointInternal: coord={coord} type={actualType} actFloor={coord.row + 1}");
            TaskHelper.RunSafely(rm.EnterMapPointInternal(coord.row + 1, actualType, null, saveGame: true));
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib.MapJump] TryGoTo({coord}) failed: {ex.Message}");
            return false;
        }
    }
}
