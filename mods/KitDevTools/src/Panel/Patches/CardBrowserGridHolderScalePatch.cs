using Godot;
using HarmonyLib;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace KitLib.Patches;

/// <summary>
/// Browser grid uses a smaller display scale than official NCardGrid (0.8).
/// Override holder scale properties like NPreviewCardHolder so hover tweens target the correct size.
/// </summary>
[HarmonyPatch(typeof(NCardHolder), "get_SmallScale")]
internal static class CardBrowserGridHolderSmallScalePatch {
    static void Postfix(NCardHolder __instance, ref Vector2 __result) {
        if (!CardBrowserUI.IsBrowserGridHolder(__instance))
            return;
        __result = CardBrowserUI.GridHolderDisplayScaleVector;
    }
}

[HarmonyPatch(typeof(NCardHolder), "get_HoverScale")]
internal static class CardBrowserGridHolderHoverScalePatch {
    static void Postfix(NCardHolder __instance, ref Vector2 __result) {
        if (!CardBrowserUI.IsBrowserGridHolder(__instance))
            return;
        __result = CardBrowserUI.GridHolderHoverScaleVector;
    }
}
