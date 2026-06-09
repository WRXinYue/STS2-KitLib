using HarmonyLib;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace KitLib.Patches;

[HarmonyPatch(typeof(ActiveScreenContext), nameof(ActiveScreenContext.GetCurrentScreen))]
public static class ActiveScreenContextModPanelPatch {
    [HarmonyPrefix]
    public static bool Prefix(ref IScreenContext? __result) {
        if (ModPanelUI.TryGetScreenContext(out var ctx)) {
            __result = ctx;
            return false;
        }
        return true;
    }
}
