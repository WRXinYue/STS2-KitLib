using System;
using Godot;
using HarmonyLib;
using KitLib;
using KitLib.Host;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.Patches;

[HarmonyPatch(typeof(NMainMenu))]
public static class MainMenuPatch {
    private static NMainMenu? _mainMenuRef;

    [HarmonyPrefix]
    [HarmonyPatch("_Ready")]
    public static void OnMainMenuReadyPrefix(NMainMenu __instance) {
        KitLib.DevPerf.KitLibRootServices.EnsureRootServicesNode();
        _mainMenuRef = __instance;
    }

    [HarmonyPostfix]
    [HarmonyPatch("_Ready")]
    public static void OnMainMenuReadyPostfix(NMainMenu __instance) {
        if (__instance != _mainMenuRef)
            return;

        BootstrapDiagnostics.FlushDeferred();

        var textRow = __instance.GetNodeOrNull<Control>("%MainMenuTextButtons")
            ?? __instance.GetNodeOrNull<Control>("MainMenuTextButtons");
        if (textRow != null) {
            foreach (var child in textRow.GetChildren()) {
                if (child is NMainMenuTextButton button) {
                    button.FocusNeighborLeft = new NodePath(".");
                    button.FocusNeighborRight = new NodePath(".");
                }
            }
        }

        if (ProgressLossPromptUI.TryShowStartupPrompt(__instance))
            return;

        if (!KitLibState.AutoProceedToCharSelect)
            return;

        KitLibState.AutoProceedToCharSelect = false;
        KitLibState.InDevRun = true;
        var charSelect = __instance.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
        charSelect.InitializeSingleplayer();
        __instance.SubmenuStack.Push(charSelect);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMainMenu.RefreshButtons))]
    public static void OnMainMenuRefreshButtons(NMainMenu __instance) {
        if (__instance != _mainMenuRef)
            return;

        if (DevMainMenuUI.IsVisible)
            DevMainMenuUI.ReapplyHide();
    }
}
