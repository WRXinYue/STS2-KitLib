using Godot;
using KitLib.Integration;
using KitLib.Settings;

namespace KitLib.Hotkeys;

/// <summary>
/// KitLib hotkey dispatch. Primary hook is <see cref="Patches.NInputManagerHotkeyPatch"/> on
/// official <c>NInputManager</c> (before keycode-only game shortcuts run).
/// </summary>
internal static class KitLibHotkeyInput {
    internal static bool TryHandleAll(InputEvent @event, Viewport viewport) {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return false;

        if (KitLibHotkeySettingsSection.TryCaptureInputEvent(key, viewport))
            return true;

        if (ModPanelHotkeys.TryHandle(key, viewport))
            return true;

        if (DevPanelHotkeys.TryHandle(key, viewport))
            return true;
        if (QuickSlHotkeys.TryHandle(key, viewport))
            return true;
        if (DevPerfHotkeys.TryHandle(key, viewport))
            return true;

        if (ShouldSuppressOfficialModifiedShortcut(key)) {
            viewport.SetInputAsHandled();
            return true;
        }

        return false;
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
