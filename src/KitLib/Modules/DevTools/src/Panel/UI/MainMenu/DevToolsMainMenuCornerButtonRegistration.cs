using System;
using Godot;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

/// <summary>
/// Publishes every Dev Mode action as its own left-rail entry, so the rail is a flat text list
/// instead of a single "Dev Mode" button opening a nested menu panel. New Test lives on the
/// character select surface so a test run can start from the vanilla character flow.
/// </summary>
internal static class DevToolsMainMenuCornerButtonRegistration {
    internal static void Register() {
        RegisterAction("dev-new-test", "New Test", "devmenu.newTest", 0, DevMainMenuUI.StartNewTestFromCharSelect,
            surface: KitLibMainMenuSurface.CharacterSelect);
        RegisterAction("dev-new-test-seed", "New Test (Seed)", "devmenu.newTestWithSeed", 1,
            DevMainMenuUI.StartNewTestWithSeed);
        RegisterAction("dev-autoslay", "AutoSlay", "devmenu.autoslay", 2, DevMainMenuUI.StartAutoSlay,
            isVisible: _ => DevMainMenuUI.AutoSlayAvailable);
        RegisterAction("dev-load-save", "Load Save", "devmenu.loadSnapshot", 3, DevMainMenuUI.LoadSave,
            isVisible: _ => DevMainMenuUI.HasAnySaveSlot);
        RegisterAction("dev-load-feedback", "Load Feedback ZIP", "devmenu.loadFeedback", 4,
            DevMainMenuUI.LoadFeedbackZip);
        RegisterAction("dev-official-replay", "Play Official Replay", "devmenu.replay", 5,
            DevMainMenuUI.PlayOfficialReplay);
        RegisterAction("dev-run-replay", "Play DevTools Replay", "devmenu.runReplay", 6,
            DevMainMenuUI.PlayDevToolsReplay);
        RegisterAction("dev-pseudo-coop", "Pseudo Co-op Test (Host)", "devmenu.pseudocoop", 7,
            DevMainMenuUI.StartPseudoCoop);
        RegisterAction("dev-unlock-all", "Unlock All Progress", "devmenu.unlockAll", 8,
            DevMainMenuUI.UnlockAllProgress);
        RegisterAction("dev-logs", "Logs", "devmenu.logs", 9, DevMainMenuUI.ShowLogs,
            isOpen: _ => LogViewerUI.IsVisible);
    }

    static void RegisterAction(string buttonId, string tooltip, string tooltipKey, int sortOrder,
        Action<NMainMenu> action, Func<object, bool>? isVisible = null, Func<object, bool>? isOpen = null,
        KitLibMainMenuSurface surface = KitLibMainMenuSurface.MainMenu) {
        KitLibMainMenuCornerButtonRegistry.Register(new KitLibMainMenuCornerButtonRegistration {
            ModId = KitLibProductIds.KitLib,
            ButtonId = buttonId,
            Tooltip = tooltip,
            TooltipKey = tooltipKey,
            SortOrder = sortOrder,
            Surface = surface,
            IsVisible = isVisible,
            IsOpen = isOpen ?? (_ => false),
            OnPressed = mainMenuObj => {
                if (mainMenuObj is NMainMenu mainMenu && GodotObject.IsInstanceValid(mainMenu))
                    action(mainMenu);
            },
        });
    }
}
