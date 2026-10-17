namespace KitLib.UI;

/// <summary>
/// Geometry shared by the two left-edge rails: the in-run rail in <c>KitLib.Panel</c> (DevPanelUI)
/// and the main-menu rail (<see cref="MainMenuRailHost"/>). The rails differ in width (icon-only in
/// run, icon + tool name on the main menu) but share the same widened black left contour band and
/// column offset, so the chrome lines up across screens.
/// </summary>
internal static class KitLibRailChrome {
    /// <summary>Left padding inside the left contour band, measured from the screen edge (x=0). The band
    /// itself is the widened black left border of the screen frame and hosts the rail column.</summary>
    internal const float RailLeft = 24f;
    /// <summary>Rail row height; the drawable icon is smaller (see MainMenuRailHost).</summary>
    internal const float IconButtonSize = 36f;
    /// <summary>Left rail chrome — above KitLib browser panels, below tool modals.</summary>
    internal const int ChromeZIndex = 1260;
}
