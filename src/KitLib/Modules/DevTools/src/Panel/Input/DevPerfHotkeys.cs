using Godot;
using KitLib.DevPerf;
using KitLib.Settings;
using KitLib.UI;

namespace KitLib.Hotkeys;

internal static class DevPerfHotkeys {
    internal static bool TryHandle(InputEventKey key, Viewport viewport) {
        if (!key.Pressed || key.Echo)
            return false;

        if (!SettingsStore.Current.HotkeyTogglePerfHud.Matches(key))
            return false;

        if (!SettingsStore.Current.HotkeysEnabled) {
            viewport.SetInputAsHandled();
            return true;
        }

        if (!KitLibState.IsActive) {
            viewport.SetInputAsHandled();
            return true;
        }

        SettingsStore.SetPerfHudEnabled(!SettingsStore.Current.PerfHudEnabled);
        KitLibRootServices.EnsureRootServicesNode();
        DevPerfOverlayUI.SyncVisibility();
        viewport.SetInputAsHandled();
        return true;
    }
}
