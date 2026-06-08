using KitLib.Settings;
using KitLib.UI;
using Godot;

namespace KitLib.Hotkeys;

/// <summary>Keyboard shortcuts for DevMode rail shell actions (no InputMap registration).</summary>
internal static class DevPanelHotkeys {
    internal static bool TryHandle(InputEvent @event, Viewport viewport) {
        if (!SettingsStore.Current.HotkeysEnabled)
            return false;
        if (!DevPanelUI.IsRailAttached)
            return false;
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return false;

        var settings = SettingsStore.Current;

        if (settings.HotkeyClosePanel.Matches(key) && DevPanelUI.HasOpenPanel) {
            DevPanelUI.CloseActivePanel();
            viewport.SetInputAsHandled();
            return true;
        }

        if (settings.HotkeyToggleRail.Matches(key)) {
            DevPanelUI.ToggleRailExpanded();
            viewport.SetInputAsHandled();
            return true;
        }

        if (settings.HotkeyNextTab.Matches(key)) {
            DevPanelUI.CycleRailTab(+1);
            viewport.SetInputAsHandled();
            return true;
        }

        if (settings.HotkeyPrevTab.Matches(key)) {
            DevPanelUI.CycleRailTab(-1);
            viewport.SetInputAsHandled();
            return true;
        }

        if (settings.HotkeyLockRail.Matches(key)) {
            DevPanelUI.ToggleRailKeyboardPin();
            viewport.SetInputAsHandled();
            return true;
        }

        return false;
    }
}
