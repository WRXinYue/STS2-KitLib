using Godot;
using KitLib.Settings;

namespace KitLib.UI;

/// <summary>
/// Immutable set of color tokens that define a visual theme for the DevMode UI.
/// </summary>
internal sealed record ThemeDefinition(
    // Panel / overlay backgrounds
    Color PanelBg,
    Color PanelBorder,

    // Text / label tones
    Color Subtle,
    Color Separator,

    // Accent
    Color Accent,
    Color AccentAlpha,

    // Rail-specific
    Color RailBg,
    Color RailBorder,
    Color IconNormal,
    Color IconHover,
    Color IconActiveBg,

    // Widget foreground / surface colors
    Color TextPrimary,
    Color TextSecondary,
    Color ButtonBgNormal,
    Color ButtonBgHover
) {
    // ── Built-in themes ────────────────────────────────────────────────────

    /// <summary>Dark — the original Apple-style dark palette.</summary>
    public static readonly ThemeDefinition Dark = new(
        PanelBg: new(0.11f, 0.11f, 0.14f, 0.96f),
        PanelBorder: new(1f, 1f, 1f, 0.08f),
        Subtle: new(0.50f, 0.50f, 0.58f, 1f),
        Separator: new(1f, 1f, 1f, 0.06f),
        Accent: new(0.40f, 0.68f, 1f, 1f),
        AccentAlpha: new(0.40f, 0.68f, 1f, 0.85f),
        RailBg: new(0.10f, 0.10f, 0.12f, 0.88f),
        RailBorder: new(1f, 1f, 1f, 0.06f),
        IconNormal: new(0.62f, 0.62f, 0.68f, 1f),
        IconHover: new(0.85f, 0.85f, 0.92f, 1f),
        IconActiveBg: new(0.40f, 0.68f, 1f, 0.15f),
        TextPrimary: new(0.88f, 0.88f, 0.94f, 1f),
        TextSecondary: new(0.75f, 0.75f, 0.82f, 1f),
        ButtonBgNormal: new(1f, 1f, 1f, 0.06f),
        ButtonBgHover: new(1f, 1f, 1f, 0.10f)
    );

    /// <summary>OLED Black — near-zero backgrounds for OLED displays.</summary>
    public static readonly ThemeDefinition Oled = new(
        PanelBg: new(0.04f, 0.04f, 0.05f, 0.98f),
        PanelBorder: new(1f, 1f, 1f, 0.10f),
        Subtle: new(0.45f, 0.45f, 0.52f, 1f),
        Separator: new(1f, 1f, 1f, 0.07f),
        Accent: new(0.45f, 0.72f, 1f, 1f),
        AccentAlpha: new(0.45f, 0.72f, 1f, 0.85f),
        RailBg: new(0.02f, 0.02f, 0.03f, 0.96f),
        RailBorder: new(1f, 1f, 1f, 0.08f),
        IconNormal: new(0.55f, 0.55f, 0.62f, 1f),
        IconHover: new(0.82f, 0.82f, 0.90f, 1f),
        IconActiveBg: new(0.051f, 0.051f, 0.051f, 1f),
        TextPrimary: new(0.90f, 0.90f, 0.96f, 1f),
        TextSecondary: new(0.72f, 0.72f, 0.80f, 1f),
        ButtonBgNormal: new(1f, 1f, 1f, 0.07f),
        ButtonBgHover: new(1f, 1f, 1f, 0.12f)
    );

    /// <summary>Light — clean light gray background with blue accent.</summary>
    public static readonly ThemeDefinition Light = new(
        PanelBg: new(0.95f, 0.95f, 0.97f, 0.97f),
        PanelBorder: new(0f, 0f, 0f, 0.10f),
        Subtle: new(0.42f, 0.42f, 0.48f, 1f),
        Separator: new(0f, 0f, 0f, 0.08f),
        Accent: new(0.12f, 0.48f, 0.90f, 1f),
        AccentAlpha: new(0.12f, 0.48f, 0.90f, 0.85f),
        RailBg: new(0.92f, 0.92f, 0.95f, 0.95f),
        RailBorder: new(0f, 0f, 0f, 0.08f),
        IconNormal: new(0.38f, 0.38f, 0.44f, 1f),
        IconHover: new(0.10f, 0.10f, 0.14f, 1f),
        IconActiveBg: new(0.12f, 0.48f, 0.90f, 0.15f),
        TextPrimary: new(0.10f, 0.10f, 0.14f, 1f),
        TextSecondary: new(0.28f, 0.28f, 0.34f, 1f),
        ButtonBgNormal: new(0f, 0f, 0f, 0.06f),
        ButtonBgHover: new(0f, 0f, 0f, 0.10f)
    );

    /// <summary>Warm — warm off-white background with amber accent.</summary>
    public static readonly ThemeDefinition Warm = new(
        PanelBg: new(0.97f, 0.95f, 0.90f, 0.97f),
        PanelBorder: new(0.20f, 0.12f, 0f, 0.12f),
        Subtle: new(0.46f, 0.38f, 0.28f, 1f),
        Separator: new(0.20f, 0.12f, 0f, 0.10f),
        Accent: new(0.82f, 0.55f, 0.10f, 1f),
        AccentAlpha: new(0.82f, 0.55f, 0.10f, 0.85f),
        RailBg: new(0.94f, 0.90f, 0.84f, 0.95f),
        RailBorder: new(0.20f, 0.12f, 0f, 0.10f),
        IconNormal: new(0.44f, 0.36f, 0.26f, 1f),
        IconHover: new(0.18f, 0.12f, 0.06f, 1f),
        IconActiveBg: new(0.82f, 0.55f, 0.10f, 0.18f),
        TextPrimary: new(0.16f, 0.10f, 0.04f, 1f),
        TextSecondary: new(0.34f, 0.26f, 0.16f, 1f),
        ButtonBgNormal: new(0.20f, 0.12f, 0f, 0.06f),
        ButtonBgHover: new(0.20f, 0.12f, 0f, 0.10f)
    );

    // ── Lookup ────────────────────────────────────────────────────────────

    public ThemeDefinition WithAccent(Color accent) => this with {
        Accent = accent,
        AccentAlpha = new Color(accent.R, accent.G, accent.B, 0.85f),
        IconActiveBg = new Color(accent.R, accent.G, accent.B, 0.15f),
    };

    public static ThemeDefinition FromName(string name) => name switch {
        ThemeNames.Oled => Oled,
        ThemeNames.Light => Light,
        ThemeNames.Warm => Warm,
        _ => Dark   // fallback / ThemeNames.Dark
    };
}
