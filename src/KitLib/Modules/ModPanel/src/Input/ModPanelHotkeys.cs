using Godot;
using KitLib.Integration;
using KitLib.Settings;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Hotkeys;

/// <summary>Global shortcut to open or close the KitLib mod settings panel.</summary>
internal static class ModPanelHotkeys {
    internal static bool TryHandle(InputEventKey key, Viewport viewport) {
        if (!key.Pressed || key.Echo)
            return false;

        if (KitLibHotkeySettingsSection.TryCaptureInputEvent(key, viewport))
            return true;

        if (!SettingsStore.Current.HotkeysEnabled)
            return false;

        if (!SettingsStore.Current.HotkeyOpenModPanel.Matches(key))
            return false;

        if (ModPanelUI.IsVisible) {
            ModPanelUI.Hide();
            viewport.SetInputAsHandled();
            return true;
        }

        var stack = ResolveSubmenuStack();
        if (stack == null)
            return false;

        ModPanelUI.Show(stack);
        viewport.SetInputAsHandled();
        return true;
    }

    static NSubmenuStack? ResolveSubmenuStack() {
        if (RunManager.Instance?.IsInProgress == true)
            return TryGetInRunStack();
        return NGame.Instance?.MainMenu?.SubmenuStack;
    }

    static NSubmenuStack? TryGetInRunStack() {
        var capstone = NRun.Instance?.GlobalUi?.SubmenuStack;
        if (capstone?.Stack == null)
            return null;
        return EnsureCapstoneVisible(capstone) ? capstone.Stack : null;
    }

    static bool EnsureCapstoneVisible(NCapstoneSubmenuStack capstone) {
        if (NCapstoneContainer.Instance?.CurrentCapstoneScreen == capstone)
            return true;

        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null)
            return false;

        var screen = capstone.ShowScreen(CapstoneSubmenuType.PauseMenu);
        if (screen is NPauseMenu pauseMenu)
            pauseMenu.Initialize(state);
        return true;
    }
}
