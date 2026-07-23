namespace KitLib.Host;

/// <summary>Panel-backed pseudo-coop UI hooks registered by KitLib.Panel at init.</summary>
public static class KitLibPseudoCoopOps {
    public static Action<object?>? EnsureGlobalUiProcessNode { get; set; }
    public static Action? AttachDeferredDevPanel { get; set; }
    public static Action? AttachDualInstanceMinimalDevPanel { get; set; }
    public static Func<bool>? IsDevPanelRailAttached { get; set; }
    public static Action<string>? EnsureMultiplayerDevActive { get; set; }
}
