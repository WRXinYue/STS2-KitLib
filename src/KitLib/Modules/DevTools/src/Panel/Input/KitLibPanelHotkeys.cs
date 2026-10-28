using Godot;

namespace KitLib.Hotkeys;

/// <summary>Panel-owned shortcuts, reached from Core's dispatcher through <see cref="Host.KitLibPanelHotkeyOps"/>.</summary>
internal static class KitLibPanelHotkeys {
    internal static bool TryHandleAll(InputEvent @event, Viewport viewport) {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return false;

        if (DevPanelHotkeys.TryHandle(key, viewport))
            return true;
        if (QuickSlHotkeys.TryHandle(key, viewport))
            return true;
        if (DevPerfHotkeys.TryHandle(key, viewport))
            return true;

        return false;
    }
}
