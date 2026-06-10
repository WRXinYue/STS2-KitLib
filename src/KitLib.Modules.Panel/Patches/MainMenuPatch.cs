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
    private static NMainMenuTextButton? _devModeButton;
    private static NMainMenu? _mainMenuRef;

    [HarmonyPrefix]
    [HarmonyPatch("_Ready")]
    public static void AddDevModeButtonPrefix(NMainMenu __instance) {
        KitLib.DevPerf.KitLibRootServices.EnsureRootServicesNode();
        _mainMenuRef = __instance;

        var settingsBtn = __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/SettingsButton");
        if (settingsBtn == null)
            return;

        var container = settingsBtn.GetParent();

        _devModeButton = MainMenuTextButtonFactory.CreateFrom(
            settingsBtn,
            container,
            "KitLibButton",
            I18N.T("menu.developerMode", "DEVMODE"),
            OnDevModeButtonPressed);

        var quitBtn = __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/QuitButton");
        int insertAt = quitBtn != null ? quitBtn.GetIndex() : container.GetChildCount();
        container.MoveChild(_devModeButton, insertAt);
    }

    [HarmonyPostfix]
    [HarmonyPatch("_Ready")]
    public static void AddDevModeButtonPostfix(NMainMenu __instance) {
        if (__instance != _mainMenuRef)
            return;

        BootstrapDiagnostics.FlushDeferred();

        if (_devModeButton != null && GodotObject.IsInstanceValid(_devModeButton)) {
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
        }

        if (ProgressLossPromptUI.TryShowStartupPrompt(__instance))
            return;

        if (CrashRecoveryPromptUI.TryShowStartupPrompt(__instance))
            return;


        if (_devModeButton == null || !GodotObject.IsInstanceValid(_devModeButton))
            return;

        if (KitLibState.AutoProceedToCharSelect) {
            KitLibState.AutoProceedToCharSelect = false;
            KitLibState.InDevRun = true;
            var charSelect = __instance.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
            charSelect.InitializeSingleplayer();
            __instance.SubmenuStack.Push(charSelect);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMainMenu.RefreshButtons))]
    public static void KeepDevButtonVisible(NMainMenu __instance) {
        if (__instance != _mainMenuRef) return;

        var settingsBtn = __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/SettingsButton");
        if (settingsBtn != null && _devModeButton != null && GodotObject.IsInstanceValid(_devModeButton))
            _devModeButton.Visible = settingsBtn.Visible;

        if (DevMainMenuUI.IsVisible)
            DevMainMenuUI.ReapplyHide();
    }

    private static void OnDevModeButtonPressed(NButton _) {
        if (_mainMenuRef == null) return;

        DevMainMenuUI.Show(_mainMenuRef, new DevMainMenuActions {
            OnNewTest = () => {
                KitLibState.InDevRun = true;
                var charSelect = _mainMenuRef.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
                charSelect.InitializeSingleplayer();
                _mainMenuRef.SubmenuStack.Push(charSelect);
            },
        });
    }
}
