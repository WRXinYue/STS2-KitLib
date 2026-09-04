using System;
using Godot;
using HarmonyLib;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.Patches;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class MainMenuCornerButtonReadyPatch {
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void Postfix(NMainMenu __instance) {
        Attach(__instance);
        Callable.From(() => Attach(__instance)).CallDeferred();
    }

    static void Attach(NMainMenu mainMenu) {
        try {
            if (!GodotObject.IsInstanceValid(mainMenu))
                return;
            MainMenuCornerButtonHost.EnsureAttached(mainMenu);
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

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu.RefreshButtons))]
internal static class MainMenuCornerButtonRefreshPatch {
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void Postfix(NMainMenu __instance) {
        try {
            MainMenuCornerButtonHost.EnsureAttached(__instance);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib main-menu corner buttons failed to refresh: {ex.Message}");
        }
    }
}
