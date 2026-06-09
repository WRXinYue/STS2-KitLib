using Godot;
using HarmonyLib;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.Patches;

[HarmonyPatch(typeof(NMainMenu))]
public static class ModMainMenuPatch {
    private static NMainMenuTextButton? _modPanelButton;
    private static NMainMenu? _mainMenuRef;

    [HarmonyPrefix]
    [HarmonyPatch("_Ready")]
    public static void AddModPanelButtonPrefix(NMainMenu __instance) {
        _mainMenuRef = __instance;

        var settingsBtn = __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/SettingsButton");
        if (settingsBtn == null) {
            MainFile.Logger.Warn("KitLib ModPanel: Could not find Settings button.");
            return;
        }

        var container = settingsBtn.GetParent();

        _modPanelButton = MainMenuTextButtonFactory.CreateFrom(
            settingsBtn,
            container,
            "ModPanelButton",
            I18N.T("menu.modPanel", "Mods"),
            OnModPanelButtonPressed);

        var quitBtn = __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/QuitButton");
        int insertAt = quitBtn != null ? quitBtn.GetIndex() : container.GetChildCount();
        container.MoveChild(_modPanelButton, insertAt);

        MainFile.Logger.Info("KitLib ModPanel: Main menu Mods button added.");
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMainMenu.RefreshButtons))]
    public static void KeepModButtonVisible(NMainMenu __instance) {
        if (__instance != _mainMenuRef) return;

        var settingsBtn = __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/SettingsButton");
        if (settingsBtn != null && _modPanelButton != null && GodotObject.IsInstanceValid(_modPanelButton))
            _modPanelButton.Visible = settingsBtn.Visible;
    }

    private static void OnModPanelButtonPressed(NButton _) {
        if (_mainMenuRef == null)
            return;

        MainFile.Logger.Info("KitLib: Opening mod panel…");
        ModPanelUI.Show(_mainMenuRef);
    }
}
