using System;
using Godot;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

/// <summary>
/// Publishes every Dev Mode action as its own left-rail entry, so the rail is a flat text list
/// instead of a single "Dev Mode" button opening a nested menu panel. Each entry is bound to the
/// menu surface it belongs to (title / character select / custom run / multiplayer).
/// </summary>
internal static class DevToolsMainMenuCornerButtonRegistration {
    internal static void Register() {
        RegisterAction("dev-new-test", "Test Mode", "devmenu.testMode", 0, DevMainMenuUI.StartNewTestFromCharSelect,
            surface: KitLibMainMenuSurface.CharacterSelect, singleplayerOnly: true);
        RegisterAction("dev-custom-test-mode", "Test Mode", "devmenu.testMode", 0,
            DevMainMenuUI.StartCustomRunTestMode, surface: KitLibMainMenuSurface.CustomRun, singleplayerOnly: true);
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
            DevMainMenuUI.StartPseudoCoop, surface: KitLibMainMenuSurface.Multiplayer);
        RegisterAction("dev-unlock-all", "Unlock All Progress", "devmenu.unlockAll", 8,
            DevMainMenuUI.UnlockAllProgress);
        RegisterAction("dev-logs", "Logs", "devmenu.logs", 9, DevMainMenuUI.ShowLogs,
            isOpen: _ => LogViewerUI.IsVisible);
    }

    static void RegisterAction(string buttonId, string tooltip, string tooltipKey, int sortOrder,
        Action<NMainMenu> action, Func<object, bool>? isVisible = null, Func<object, bool>? isOpen = null,
        KitLibMainMenuSurface surface = KitLibMainMenuSurface.MainMenu, bool singleplayerOnly = false) {
        KitLibMainMenuCornerButtonRegistry.Register(new KitLibMainMenuCornerButtonRegistration {
            ModId = KitLibProductIds.KitLib,
            ButtonId = buttonId,
            Tooltip = tooltip,
            TooltipKey = tooltipKey,
            SortOrder = sortOrder,
            Surface = surface,
            SingleplayerOnly = singleplayerOnly,
            IsVisible = isVisible,
            IsOpen = isOpen ?? (_ => false),
            OnPressed = mainMenuObj => {
                if (mainMenuObj is NMainMenu mainMenu && GodotObject.IsInstanceValid(mainMenu))
                    action(mainMenu);
            },
        });
    }
}
