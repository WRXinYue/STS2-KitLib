using Godot;
using KitLib.UI;

namespace KitLib.Integration;
/// <summary>
/// DevMode look for RitsuLib-built settings rows (sliders, line edits, buttons, option rows).
/// Applied after page content is instantiated; does not modify RitsuLib sources.
/// </summary>
internal static class ModSettingsRitsuFormDevTheme {
    private const int CornerMini = 8;
    public static void ApplyToSubtree(Control root) {
        if (!GodotObject.IsInstanceValid(root))
            return;
        ApplyRecursive(root);
    }
    private static void ApplyRecursive(Node node) {
        switch (node) {
            case HSlider s:
                StyleSlider(s);
                break;
            case VSlider s:
                StyleSlider(s);
                break;
            case LineEdit le:
                StyleLineEdit(le);
                break;
            case TextEdit te:
                StyleTextEdit(te);
                break;
            case OptionButton ob:
                StyleOptionButtonLike(ob);
                break;
            case MenuButton mb:
                StylePushButton(mb);
                break;
            case CheckBox cb:
                StyleCheckBoxOnlyColors(cb);
                break;
            case ColorPickerButton cp:
                StylePushButton(cp);
                break;
            case Button b:
                StylePushButton(b);
                break;
        }
        foreach (var child in node.GetChildren())
            ApplyRecursive(child);
    }
    private static StyleBoxFlat Dup(StyleBoxFlat s) => (StyleBoxFlat)s.Duplicate();
    private static void StyleLineEdit(LineEdit le) {
        le.AddThemeStyleboxOverride("normal", DevModeFormChrome.RoundedField(false));
        le.AddThemeStyleboxOverride("read_only", DevModeFormChrome.RoundedField(false));
        var focus = DevModeFormChrome.RoundedField(true);
        le.AddThemeStyleboxOverride("focus", focus);
        le.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        le.AddThemeColorOverride("font_placeholder_color", KitLibTheme.Subtle);
        le.AddThemeColorOverride("caret_color", DevModeFormChrome.RoundedFieldCaretColor());
        le.AddThemeFontSizeOverride("font_size", 14);
        DevModeFormChrome.WireRoundedFieldFocusMotion(le);
    }
    private static void StyleTextEdit(TextEdit te) {
        te.AddThemeStyleboxOverride("normal", DevModeFormChrome.RoundedField(false));
        te.AddThemeStyleboxOverride("read_only", DevModeFormChrome.RoundedField(false));
        te.AddThemeStyleboxOverride("focus", DevModeFormChrome.RoundedField(true));
        te.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        te.AddThemeColorOverride("caret_color", DevModeFormChrome.RoundedFieldCaretColor());
        te.AddThemeFontSizeOverride("font_size", 14);
        DevModeFormChrome.WireRoundedFieldFocusMotion(te);
    }
    private static void StyleOptionButtonLike(Control ob) {
        var n = DevModeFormChrome.RoundedField(false);
        var h = Dup(n);
        var c = h.BgColor;
        h.BgColor = new Color(c.R, c.G, c.B, Mathf.Min(c.A * 1.3f, 0.36f));
        var p = Dup(n);
        p.BorderColor = DevModeFormChrome.RoundedFieldBorderColor(true);
        p.BorderWidthLeft = p.BorderWidthTop = p.BorderWidthRight = p.BorderWidthBottom = 2;
        var f = DevModeFormChrome.RoundedField(true);
        ob.AddThemeStyleboxOverride("normal", n);
        ob.AddThemeStyleboxOverride("hover", h);
        ob.AddThemeStyleboxOverride("pressed", p);
        ob.AddThemeStyleboxOverride("focus", f);
        ob.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        ob.AddThemeColorOverride("font_hover_color", KitLibTheme.TextPrimary);
        ob.AddThemeColorOverride("font_pressed_color", KitLibTheme.TextPrimary);
        ob.AddThemeFontSizeOverride("font_size", 14);
        DevModeFormChrome.WireRoundedFieldFocusMotion(ob);
    }
    private static StyleBoxFlat MiniButtonBox(bool hover, bool pressed) {
        var bg = pressed
            ? new Color(KitLibTheme.Accent.R, KitLibTheme.Accent.G, KitLibTheme.Accent.B, 0.35f)
            : hover
                ? KitLibTheme.ButtonBgHover
                : KitLibTheme.ButtonBgNormal;
        return new StyleBoxFlat {
            BgColor = new Color(bg.R, bg.G, bg.B, pressed ? 0.9f : 0.92f),
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = CornerMini,
            CornerRadiusTopRight = CornerMini,
            CornerRadiusBottomRight = CornerMini,
            CornerRadiusBottomLeft = CornerMini,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            ShadowSize = 0,
        };
    }
    private static StyleBoxFlat LargeButtonBox(bool hover, bool pressed) {
        var bg = pressed
            ? new Color(KitLibTheme.Accent.R, KitLibTheme.Accent.G, KitLibTheme.Accent.B, 0.28f)
            : hover
                ? KitLibTheme.ButtonBgHover
                : KitLibTheme.ButtonBgNormal;
        return new StyleBoxFlat {
            BgColor = new Color(bg.R, bg.G, bg.B, pressed ? 0.88f : 0.94f),
            BorderColor = pressed ? KitLibTheme.Accent : KitLibTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusBottomLeft = 8,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            ShadowSize = 0,
        };
    }
    private static void StylePushButton(Button b) {
        var mini = b.CustomMinimumSize is { X: > 0, Y: > 0 } sz && sz.X <= 48f && sz.Y <= 48f;
        if (mini) {
            b.AddThemeStyleboxOverride("normal", MiniButtonBox(false, false));
            b.AddThemeStyleboxOverride("hover", MiniButtonBox(true, false));
            b.AddThemeStyleboxOverride("pressed", MiniButtonBox(false, true));
            b.AddThemeStyleboxOverride("disabled", MiniButtonBox(false, false));
            b.AddThemeFontSizeOverride("font_size", 17);
        }
        else {
            b.AddThemeStyleboxOverride("normal", LargeButtonBox(false, false));
            b.AddThemeStyleboxOverride("hover", LargeButtonBox(true, false));
            b.AddThemeStyleboxOverride("pressed", LargeButtonBox(false, true));
            b.AddThemeStyleboxOverride("disabled", LargeButtonBox(false, false));
            b.AddThemeFontSizeOverride("font_size", 15);
        }
        b.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        b.AddThemeColorOverride("font_hover_color", KitLibTheme.TextPrimary);
        b.AddThemeColorOverride("font_pressed_color", KitLibTheme.TextPrimary);
        b.AddThemeColorOverride("font_disabled_color", KitLibTheme.Subtle);
    }
    private static void StyleCheckBoxOnlyColors(CheckBox cb) {
        cb.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        cb.AddThemeColorOverride("font_hover_color", KitLibTheme.TextPrimary);
        cb.AddThemeColorOverride("font_pressed_color", KitLibTheme.TextPrimary);
        cb.AddThemeFontSizeOverride("font_size", 14);
    }
    private static void StyleSlider(Slider s) => DevModeFormChrome.ApplySliderStyle(s);
}