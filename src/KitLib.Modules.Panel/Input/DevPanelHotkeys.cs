using Godot;
using KitLib.Settings;
using KitLib.UI;

namespace KitLib.Hotkeys;

/// <summary>Keyboard shortcuts for DevMode rail shell actions (no InputMap registration).</summary>
internal static class DevPanelHotkeys {
    internal static bool TryHandle(InputEventKey key, Viewport viewport) {
        if (!key.Pressed || key.Echo)
            return false;

        if (!SettingsStore.Current.HotkeysEnabled)
            return false;

        if (!DevPanelUI.IsRailAttached)
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
