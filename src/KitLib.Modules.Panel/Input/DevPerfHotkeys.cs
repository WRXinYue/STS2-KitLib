using Godot;
using KitLib.DevPerf;
using KitLib.Settings;
using KitLib.UI;

namespace KitLib.Hotkeys;

internal static class DevPerfHotkeys {
    internal static bool TryHandle(InputEvent @event, Viewport viewport) {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return false;

        if (!SettingsStore.Current.HotkeyTogglePerfHud.Matches(key))
            return false;

        if (!SettingsStore.Current.HotkeysEnabled) {
            MainFile.Logger.Info("[Perf] F3 ignored: keyboard shortcuts disabled in settings.");
            viewport.SetInputAsHandled();
            return true;
        }

        if (!KitLibState.IsActive) {
            MainFile.Logger.Info("[Perf] F3 ignored: DevMode inactive (enable DevPanel/Cheat on normal runs or start a dev test run).");
            viewport.SetInputAsHandled();
            return true;
        }

        bool next = !SettingsStore.Current.PerfHudEnabled;
        SettingsStore.SetPerfHudEnabled(next);
        KitLibRootServices.EnsureRootServicesNode();
        DevPerfOverlayUI.SyncVisibility();
        MainFile.Logger.Info($"[Perf] Overlay toggled {(next ? "ON" : "OFF")} via hotkey.");
        viewport.SetInputAsHandled();
        return true;
    }
}
