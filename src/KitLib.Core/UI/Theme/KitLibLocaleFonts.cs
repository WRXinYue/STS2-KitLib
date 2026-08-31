using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.TestSupport;

namespace KitLib.UI;

/// <summary>
/// STS2 swaps Kreon for bundled locale fonts (CJK, etc.) via
/// <see cref="FontManager"/>. KitLib overlays are not under a themed menu root,
/// so they must apply the same substitution instead of relying on OS fallback.
/// </summary>
internal static class KitLibLocaleFonts {
    public static void ApplyMegaRichText(MegaRichTextLabel label) {
        var placeholder = ThemeDB.FallbackFont;
        if (placeholder != null) {
            label.AddThemeFontOverride(ThemeConstants.RichTextLabel.NormalFont, placeholder);
            label.AddThemeFontOverride(ThemeConstants.RichTextLabel.BoldFont, placeholder);
            label.AddThemeFontOverride(ThemeConstants.RichTextLabel.ItalicsFont, placeholder);
            label.AddThemeFontOverride("bold_italics_font", placeholder);
            label.AddThemeFontOverride("mono_font", placeholder);
        }

        Apply(label, FontType.Regular, ThemeConstants.RichTextLabel.NormalFont);
        Apply(label, FontType.Bold, ThemeConstants.RichTextLabel.BoldFont);
        Apply(label, FontType.Italic, ThemeConstants.RichTextLabel.ItalicsFont);
        Apply(label, FontType.Bold, "bold_italics_font");
        Apply(label, FontType.Regular, "mono_font");
    }

    public static void ApplyMegaLabel(MegaLabel label, FontType type = FontType.Regular) {
        Apply(label, type, ThemeConstants.Label.Font);
    }

    public static void ApplyControl(Control control, FontType type = FontType.Regular) {
        switch (control) {
            case MegaRichTextLabel megaRtl:
                ApplyMegaRichText(megaRtl);
                return;
            case RichTextLabel rtl:
                Apply(rtl, FontType.Regular, ThemeConstants.RichTextLabel.NormalFont);
                Apply(rtl, FontType.Bold, ThemeConstants.RichTextLabel.BoldFont);
                Apply(rtl, FontType.Italic, ThemeConstants.RichTextLabel.ItalicsFont);
                return;
            case LineEdit:
                Apply(control, type, ThemeConstants.LineEdit.Font);
                return;
            case TextEdit:
                Apply(control, type, ThemeConstants.TextEdit.Font);
                return;
            default:
                Apply(control, type, ThemeConstants.Label.Font);
                return;
        }
    }

    public static Theme? CreateOverlayTheme() {
        var regular = TryGetFont(FontType.Regular);
        if (regular == null)
            return null;

        var bold = TryGetFont(FontType.Bold) ?? regular;
        var italic = TryGetFont(FontType.Italic) ?? regular;
        var theme = new Theme { DefaultFont = regular };
        foreach (var type in new[] { "Label", "Button", "CheckBox", "OptionButton" })
            theme.SetFont("font", type, regular);
        theme.SetFont("font", "LineEdit", regular);
        theme.SetFont("font", "TextEdit", regular);
        theme.SetFont("normal_font", "RichTextLabel", regular);
        theme.SetFont("bold_font", "RichTextLabel", bold);
        theme.SetFont("italics_font", "RichTextLabel", italic);
        theme.SetFont("bold_italics_font", "RichTextLabel", bold);
        theme.SetFont("mono_font", "RichTextLabel", regular);
        return theme;
    }

    public static void Apply(Control control, FontType fontType, StringName themeFontName) {
        var font = TryGetFont(fontType);
        if (font != null)
            control.AddThemeFontOverride(themeFontName, font);
    }

    static Font? TryGetFont(FontType type) {
        if (Engine.IsEditorHint() || TestMode.IsOn)
            return null;

        var language = TryLanguage();
        if (string.IsNullOrWhiteSpace(language) || !FontManager.NeedsFontSubstitution(language))
            return null;

        return FontManager.GetSubstituteFont(language, type);
    }

    static string? TryLanguage() {
        try {
            return LocManager.Instance?.Language;
        }
        catch {
            return null;
        }
    }
}
