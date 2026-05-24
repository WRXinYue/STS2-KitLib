using System;
using DevMode.UI;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace DevMode.Patches;

[HarmonyPatch(typeof(NMainMenu))]
public static class MainMenuPatch {
    private static NMainMenuTextButton? _devModeButton;
    private static NMainMenuTextButton? _logsButton;
    private static NMainMenuTextButton? _feedbackButton;
    private static NMainMenu? _mainMenuRef;

    [HarmonyPrefix]
    [HarmonyPatch("_Ready")]
    public static void AddDevModeButtonPrefix(NMainMenu __instance) {
        _mainMenuRef = __instance;

        var settingsBtn = __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/SettingsButton");
        if (settingsBtn == null) {
            MainFile.Logger.Warn("DevMode: Could not find Settings button.");
            return;
        }

        var container = settingsBtn.GetParent();

        _devModeButton = MainMenuTextButtonFactory.CreateFrom(
            settingsBtn,
            container,
            "DevModeButton",
            I18N.T("menu.developerMode", "Dev Mode (DevMod)"),
            OnDevModeButtonPressed);

        _logsButton = MainMenuTextButtonFactory.CreateFrom(
            settingsBtn,
            container,
            "DevModeLogsButton",
            I18N.T("menu.logsDevMod", "Logs (DevMod)"),
            _ => {
                if (_mainMenuRef != null)
                    LogViewerUI.ShowOnMainMenu(_mainMenuRef);
            });

        _feedbackButton = MainMenuTextButtonFactory.CreateFrom(
            settingsBtn,
            container,
            "DevModeFeedbackButton",
            I18N.T("menu.feedbackDevMod", "Mod Feedback (DevMod)"),
            _ => {
                if (_mainMenuRef != null)
                    FeedbackReportUI.ShowOnMainMenu(_mainMenuRef);
            });

        var quitBtn = __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/QuitButton");
        int insertAt = quitBtn != null ? quitBtn.GetIndex() : container.GetChildCount();
        container.MoveChild(_devModeButton, insertAt);
        container.MoveChild(_logsButton, insertAt + 1);
        container.MoveChild(_feedbackButton, insertAt + 2);

        MainFile.Logger.Info("DevMode: Main menu Developer Mode button added.");
    }

    [HarmonyPostfix]
    [HarmonyPatch("_Ready")]
    public static void AddDevModeButtonPostfix(NMainMenu __instance) {
        if (__instance != _mainMenuRef || _devModeButton == null || !GodotObject.IsInstanceValid(_devModeButton))
            return;

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

        if (DevModeState.AutoProceedToCharSelect) {
            DevModeState.AutoProceedToCharSelect = false;
            DevModeState.InDevRun = true;
            MainFile.Logger.Info("DevMode: Auto-proceeding to character select (Restart with Seed).");
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
            if (_logsButton != null && GodotObject.IsInstanceValid(_logsButton))
                _logsButton.Visible = visible;
            if (_feedbackButton != null && GodotObject.IsInstanceValid(_feedbackButton))
                _feedbackButton.Visible = visible;
        }

        if (DevMainMenuUI.IsVisible)
            DevMainMenuUI.ReapplyHide();
    }

    private static void OnDevModeButtonPressed(NButton _) {
        if (_mainMenuRef == null) return;

        MainFile.Logger.Info("DevMode: Opening dev mode menu...");

        DevMainMenuUI.Show(_mainMenuRef, new DevMainMenuActions {
            OnNewTest = () => {
                DevModeState.InDevRun = true;
                var charSelect = _mainMenuRef.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
                charSelect.InitializeSingleplayer();
                _mainMenuRef.SubmenuStack.Push(charSelect);
            },
            OnCardLibrary = () => {
                DevModeState.InMenuPreview = true;
                var stack = _mainMenuRef.SubmenuStack;
                DevModeState.OnMenuPreviewClosed = () => {
                    stack.Pop();
                    OnDevModeButtonPressed(null!);
                };
                AccessTools.Method(typeof(NMainMenu), "OpenCompendiumSubmenu")
                    ?.Invoke(_mainMenuRef, [null]);
                var compendium = stack.Peek();
                AccessTools.Method(compendium.GetType(), "OpenCardLibrary")
                    ?.Invoke(compendium, [null]);
            },
            OnRelicCollection = () => {
                DevModeState.InMenuPreview = true;
                var stack = _mainMenuRef.SubmenuStack;
                DevModeState.OnMenuPreviewClosed = () => {
                    stack.Pop();
                    OnDevModeButtonPressed(null!);
                };
                AccessTools.Method(typeof(NMainMenu), "OpenCompendiumSubmenu")
                    ?.Invoke(_mainMenuRef, [null]);
                var compendium = stack.Peek();
                AccessTools.Method(compendium.GetType(), "OpenRelicCollection")
                    ?.Invoke(compendium, [null]);
            }
        });
    }
}
