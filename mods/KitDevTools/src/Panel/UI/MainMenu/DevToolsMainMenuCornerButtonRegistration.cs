using Godot;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using KitLib.Host;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class DevToolsMainMenuCornerButtonRegistration {
    internal static void Register() {
        KitLibMainMenuCornerButtonRegistry.Register(new KitLibMainMenuCornerButtonRegistration {
            ModId = KitLibProductIds.KitDevTools,
            ButtonId = "dev-mode",
            Tooltip = "Dev Mode",
            TooltipKey = "menu.developerMode",
            SortOrder = 0,
            IsOpen = _ => DevMainMenuUI.IsVisible,
            OnPressed = OpenDevMenu,
        });
    }

    static void OpenDevMenu(object mainMenuObj) {
        if (mainMenuObj is not NMainMenu mainMenu || !GodotObject.IsInstanceValid(mainMenu))
            return;

        if (DevMainMenuUI.IsVisible) {
            DevMainMenuUI.Hide();
            return;
        }

        DevMainMenuUI.Show(mainMenu, new DevMainMenuActions {
            OnNewTest = () => {
                KitLibState.InDevRun = true;
                var charSelect = mainMenu.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
                charSelect.InitializeSingleplayer();
                mainMenu.SubmenuStack.Push(charSelect);
            },
        });
    }
}
