using KitLib.Map;
using KitLib.Multiplayer.Cheat;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.Patches.Map;

[HarmonyPatch(typeof(NMapScreen), "RecalculateTravelability")]
public static class MapTravelabilityRewritePatch {
    public static void Postfix(NMapScreen __instance) {
        if (!MapScreenUnlock.IsActive && !MpCheatApplier.FreeTravelFromDevRoomMap) {
            __instance.SetDebugTravelEnabled(false);
            return;
        }

        var dict = MapScreenReflection.GetMapPoints(__instance);
        if (dict == null || dict.Count == 0) return;

        foreach (NMapPoint point in dict.Values) {
            if (point.State == MapPointState.Untravelable)
                point.State = MapPointState.Travelable;
        }

        __instance.SetDebugTravelEnabled(true);
    }
}
