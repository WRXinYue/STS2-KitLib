namespace KitLib.Host;

/// <summary>Panel-owned overlay hooks registered by KitLib.Panel; invoked from satellite tab activators.</summary>
public static class KitLibPanelUiOps {
    public static Action<object>? ShowCheatsOverlay { get; set; }
    public static Action<object>? ShowSaveLoadOverlay { get; set; }
    public static Action<object>? ShowSettingsOverlay { get; set; }
    public static Action<object>? ShowAiOverlay { get; set; }
    public static Action<object>? SyncAiHud { get; set; }
    public static Action<object>? AttachAiHud { get; set; }
    public static Action<object>? DetachAiHud { get; set; }
}
