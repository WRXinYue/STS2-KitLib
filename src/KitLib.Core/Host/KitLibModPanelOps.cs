namespace KitLib.Host;

/// <summary>
/// Optional KitModPanel services. Null when that product is not loaded.
/// </summary>
public static class KitLibModPanelOps {
    public static Func<object, object, bool>? TryCaptureHotkeySettingsInput { get; set; }

    public static Func<object, object, bool>? TryHandleOpenModPanelHotkey { get; set; }

    public static Action? CancelHotkeySettingsCapture { get; set; }

    /// <summary>Build hotkey rebind UI (Godot Control).</summary>
    public static Func<bool, object?>? BuildHotkeySettingsSection { get; set; }
}
