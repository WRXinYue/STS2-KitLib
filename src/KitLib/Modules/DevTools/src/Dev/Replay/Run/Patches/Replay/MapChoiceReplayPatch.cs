using HarmonyLib;
using KitLib.Replay.Commands;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.Replay.Patches.Replay;

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetTravelEnabled))]
public static class MapChoiceReplayPatch {
    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance, bool enabled) {
        if (!enabled || !__instance.IsTravelEnabled)
            return;

        MapMoveCommand._activeScreen = __instance;
        ReplayDispatcher.TryDispatch();
    }
}
