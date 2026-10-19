using Godot;

namespace KitLib.UI;

/// <summary>
/// Geometry and motion shared by the two left-edge rails: the in-run rail in <c>KitLib.Panel</c>
/// (DevPanelUI) and the main-menu rail (<see cref="MainMenuRailHost"/>). The rails differ in width
/// (icon-only in run, icon + tool name on the main menu) but share the same widened black left contour
/// band, column offset and spring, so the chrome lines up and moves alike across screens.
/// </summary>
internal static class KitLibRailChrome {
    /// <summary>Left padding inside the left contour band, measured from the screen edge (x=0). The band
    /// itself is the widened black left border of the screen frame and hosts the rail column.</summary>
    internal const float RailLeft = 24f;
    /// <summary>Rail row height; the drawable icon is smaller (see MainMenuRailHost).</summary>
    internal const float IconButtonSize = 36f;
    /// <summary>Left rail chrome — above KitLib browser panels, below tool modals.</summary>
    internal const int ChromeZIndex = 1260;

    /// <summary>Spring stiffness shared by the rails' motion, in rad/s.</summary>
    internal const float SpringOmega = 15.7f;
    /// <summary>Duration of a rail spring, in seconds.</summary>
    internal const float SpringSeconds = 0.45f;
    /// <summary>Damping ratio of a rail spring; below 1 gives a single light overshoot.</summary>
    internal const float SpringZeta = 0.72f;

    /// <summary>Underdamped spring easing: one light overshoot, then a settle.</summary>
    /// <remarks>
    /// <c>y(t) = 1 - e^(-ζωt) · (cos(ωd·t) + (ζ/√(1-ζ²)) · sin(ωd·t))</c>, with <c>ωd = ω√(1-ζ²)</c>.
    /// </remarks>
    internal static float SpringEase(float seconds, float zeta) {
        float sqrtTerm = Mathf.Sqrt(1f - zeta * zeta);
        float dampedOmega = SpringOmega * sqrtTerm;
        return 1f - Mathf.Exp(-zeta * SpringOmega * seconds) *
               (Mathf.Cos(dampedOmega * seconds) + (zeta / sqrtTerm) * Mathf.Sin(dampedOmega * seconds));
    }
}
