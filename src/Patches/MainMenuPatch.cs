using System;
using KitLib.Feedback;
using KitLib.UI;
using Godot;
using HarmonyLib;
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
        CrashRecoveryHooks.EnsureLifecycleNode();
        _mainMenuRef = __instance;

        var settingsBtn = __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/SettingsButton");
        if (settingsBtn == null) {
            MainFile.Logger.Warn("KitLib: Could not find Settings button.");
            return;
        }

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

        MainFile.Logger.Info("KitLib: Main menu Developer Mode button added.");
    }

    [HarmonyPostfix]
    [HarmonyPatch("_Ready")]
    public static void AddDevModeButtonPostfix(NMainMenu __instance) {
        if (__instance != _mainMenuRef)
            return;

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
            MainFile.Logger.Info("KitLib: Auto-proceeding to character select (Restart with Seed).");
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
        if (settingsBtn != null) {
            bool visible = settingsBtn.Visible;
            if (_devModeButton != null && GodotObject.IsInstanceValid(_devModeButton))
                _devModeButton.Visible = visible;
        }

        if (DevMainMenuUI.IsVisible)
            DevMainMenuUI.ReapplyHide();
    }

    private static void OnDevModeButtonPressed(NButton _) {
        if (_mainMenuRef == null) return;

        MainFile.Logger.Info("KitLib: Opening dev mode menu...");

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
