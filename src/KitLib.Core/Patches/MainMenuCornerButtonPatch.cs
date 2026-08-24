using System;
using HarmonyLib;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.Patches;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class MainMenuCornerButtonReadyPatch {
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void Postfix(NMainMenu __instance) {
        try {
            MainMenuCornerButtonHost.EnsureAttached(__instance);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib main-menu corner buttons failed to attach: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NMainMenu), "OnSubmenuStackChanged")]
internal static class MainMenuCornerButtonSubmenuPatch {
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void Postfix(NMainMenu __instance) {
        try {
            MainMenuCornerButtonHost.SyncVisibility(__instance);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib main-menu corner buttons failed to sync: {ex.Message}");
        }
    }
}
