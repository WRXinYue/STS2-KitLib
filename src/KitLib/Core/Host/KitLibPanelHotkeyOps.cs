namespace KitLib.Host;

/// <summary>
/// Optional KitLib.Hud keyboard handlers. Null when that satellite is not loaded.
/// </summary>
public static class KitLibPanelHotkeyOps {
    public static Func<object, object, bool>? TryHandleHotkey { get; set; }
}
