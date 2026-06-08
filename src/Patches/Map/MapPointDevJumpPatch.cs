using KitLib.Map;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.Patches.Map;

/// <summary>
/// When map debug-jump is enabled, map clicks use <see cref="VanillaMapNavigator"/>.
/// Otherwise vanilla <see cref="NMapPoint.OnRelease"/> handles path travel.
/// </summary>
[HarmonyPatch(typeof(NMapPoint), "OnRelease")]
public static class MapPointDevJumpPatch {
    public static bool Prefix(NMapPoint __instance) {
        if (!MapScreenUnlock.IsActive)
            return true;

        var point = __instance.Point;
        if (point == null)
            return true;

        var screen = NMapScreen.Instance;
        if (screen != null && screen.IsTraveling)
            return true;

        if (!VanillaMapNavigator.TryGoTo(point.coord)) {
            MainFile.Logger.Warn(
                $"[KitLib.MapJump] TryGoTo failed, vanilla fallback: {point.coord} {point.PointType}");
            return true;
        }

        MapScreenUnlock.ApplyUnlock(screen);
        return false;
    }
}
