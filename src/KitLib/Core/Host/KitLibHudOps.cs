using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.Host;

/// <summary>
/// Chrome hooks owned by the Hud satellite (window frame, main-menu rail, notification island).
/// Every member stays null while that module is disabled or not installed, so Core never draws
/// KitLib chrome without it.
/// </summary>
public static class KitLibHudOps {
    /// <summary>Duration of the wrap retract / restore transition, matching the Hud module.</summary>
    public const float WrapAnimationSeconds = 0.22f;

    public static Action? EnsureScreenFrame { get; set; }

    /// <summary>Attach the frame, insets, rail, and island to a freshly built main menu.</summary>
    public static Action<NMainMenu>? AttachMainMenuChrome { get; set; }

    /// <summary>Refresh rail and island after a submenu or button rebuild.</summary>
    public static Action<NMainMenu>? SyncMainMenuChrome { get; set; }

    public static Func<NMainMenu, bool>? IsRailAttachedTo { get; set; }

    public static Func<bool>? IsRailOnScreen { get; set; }

    public static Action? SlideRailOutForEntry { get; set; }

    public static Func<bool>? HasWrapHost { get; set; }

    public static Action? ToggleWrap { get; set; }
}
