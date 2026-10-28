using Godot;
using KitLib.Host;
using KitLib.Settings;
using KitLib.UI;

namespace KitLib.Hotkeys;

/// <summary>
/// KitLib hotkey dispatch. Runs before official keycode-only shortcuts; the wrap toggle is handled here so
/// the Core frame and rail stay dismissible even when no product satellite is loaded.
/// </summary>
internal static class KitLibHotkeyInput {
    internal static bool TryHandleAll(InputEvent @event, Viewport viewport) {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return false;

        if (KitLibModPanelOps.TryCaptureHotkeySettingsInput?.Invoke(key, viewport) == true)
            return true;

        if (KitLibModPanelOps.TryHandleOpenModPanelHotkey?.Invoke(key, viewport) == true)
            return true;

        if (TryHandleWrapToggle(key, viewport))
            return true;

        if (KitLibPanelHotkeyOps.TryHandleHotkey?.Invoke(key, viewport) == true)
            return true;

        if (ShouldSuppressOfficialModifiedShortcut(key)) {
            viewport.SetInputAsHandled();
            return true;
        }

        return false;
    }

    /// <summary>Retracts or restores the whole wrap — window edge, rail and pushed chrome — in any rail scene.</summary>
    private static bool TryHandleWrapToggle(InputEventKey key, Viewport viewport) {
        var settings = SettingsStore.Current;
        if (!settings.HotkeysEnabled || !settings.HotkeyToggleRail.Matches(key))
            return false;
        if (!KitLibScreenFrame.HasWrapHost)
            return false;

        KitLibScreenFrame.ToggleWrapShown(animated: true);
        KitLibHotkeySignals.ReportUsed(HotkeyActionId.ToggleRail);
        viewport.SetInputAsHandled();
        return true;
    }

    /// <summary>
    /// Official shortcuts match keycode only; block them when Ctrl/Shift/Alt are held so
    /// modifier combos are available to KitLib (and do not trigger map etc.).
    /// </summary>
    internal static bool ShouldSuppressOfficialModifiedShortcut(InputEventKey key) {
        if (!key.CtrlPressed && !key.ShiftPressed && !key.AltPressed)
            return false;
        return OfficialGameInput.UsesPlayerKeyboardShortcut(key.Keycode);
    }
}
