using Godot;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class ModPanelMainMenuCornerButtonRegistration {
    internal static void Register() {
        KitLibMainMenuCornerButtonRegistry.Register(new KitLibMainMenuCornerButtonRegistration {
            ModId = KitLibProductIds.KitModPanel,
            ButtonId = "mod-panel",
            Tooltip = "Mod Panel",
            TooltipKey = "menu.modPanel",
            SortOrder = 10,
            IsOpen = _ => ModPanelUI.IsVisible,
            OnPressed = OpenModPanel,
        });
    }

    static void OpenModPanel(object mainMenuObj) {
        if (mainMenuObj is not NMainMenu mainMenu || !GodotObject.IsInstanceValid(mainMenu))
            return;

        if (ModPanelUI.IsVisible) {
            ModPanelUI.Hide();
            return;
        }

        ModPanelUI.Show(mainMenu);
    }
}
