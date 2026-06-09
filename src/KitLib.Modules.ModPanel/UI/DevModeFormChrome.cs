using Godot;
namespace KitLib.UI;
/// <summary>
///     Reusable form layout and control styling (labeled rows, padded cards, sliders, text fields, buttons).
///     Any DevMode screen may use this; not specific to mod settings.
/// </summary>
internal static class DevModeFormChrome {
    /// <summary>Default sizes for form rows; callers may override per-control <see cref="Control.CustomMinimumSize" />.</summary>
    public static class Metrics {
        public const float ValueColumnMinWidth = 200f;
        public const float ValueColumnMinHeight = 44f;
        public const float SliderTrackMinWidth = 220f;
        public const float SliderRowMinWidth = 348f;
        public const float SliderCaptionWidth = 72f;
        public const float SliderCaptionHeight = 40f;
        public const float ChoiceRowMinWidth = 292f;
        public const float ChoiceCenterMinWidth = 180f;
        public const float MiniIconButtonSize = 40f;
        public const float ColorSwatchSize = 40f;
        public const float ColorRowMinWidth = 300f;
        public const float StringFieldMinWidth = 320f;
        public const float StringMultilineMinHeight = 104f;
    }
    private const int FieldCornerRadius = 8;
    public static Control CreateLabeledValueRow(string title, string? description, Control value) {
        var row = new HBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        row.AddThemeConstantOverride("separation", 16);
        var left = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        left.AddThemeConstantOverride("separation", 4);
        left.AddChild(CreateTitleLabel(string.IsNullOrWhiteSpace(title) ? "—" : title));
        if (!string.IsNullOrWhiteSpace(description))
            left.AddChild(CreateDescriptionLabel(description));
        value.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        value.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        value.CustomMinimumSize = new Vector2(
            Mathf.Max(value.CustomMinimumSize.X, Metrics.ValueColumnMinWidth),
            Mathf.Max(value.CustomMinimumSize.Y, Metrics.ValueColumnMinHeight));
        row.AddChild(left);
        row.AddChild(value);
        return row;
    }
    public static Control CreateStackedField(string title, string? desc, Control field) {
        var col = new VBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        col.AddThemeConstantOverride("separation", 6);
        col.AddChild(CreateTitleLabel(string.IsNullOrWhiteSpace(title) ? "—" : title));
        if (!string.IsNullOrWhiteSpace(desc))
            col.AddChild(CreateDescriptionLabel(desc));
        field.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        field.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        col.AddChild(field);
        return col;
    }
    public static Label CreateTitleLabel(string text) {
        var l = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        l.AddThemeFontSizeOverride("font_size", 17);
        l.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        return l;
    }
    public static Label CreateDescriptionLabel(string text) {
        var l = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        l.AddThemeFontSizeOverride("font_size", 13);
        l.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        return l;
    }
    public static Label CreateSliderValueCaption() {
        var l = new Label {
            CustomMinimumSize = new Vector2(Metrics.SliderCaptionWidth, Metrics.SliderCaptionHeight),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        l.AddThemeFontSizeOverride("font_size", 14);
        l.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        return l;
    }
    public static HBoxContainer CreateSliderTrackRow(HSlider track, Label valueCaption) {
        var row = new HBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(Metrics.SliderRowMinWidth, Metrics.ValueColumnMinHeight),
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        row.AddThemeConstantOverride("separation", 10);
        track.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        valueCaption.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(track);
        row.AddChild(valueCaption);
        return row;
    }
    public static void ApplySliderStyle(Slider s) {
        const int radius = 4;
        const int vPad = 4;
        var pb = KitLibTheme.PanelBg;
        var lum = pb.R * 0.299f + pb.G * 0.587f + pb.B * 0.114f;
        var trackBg = lum > 0.5f
            ? new Color(0f, 0f, 0f, 0.07f)
            : new Color(1f, 1f, 1f, 0.11f);
        var trackEdge = lum > 0.5f
            ? new Color(0f, 0f, 0f, 0.14f)
            : new Color(1f, 1f, 1f, 0.20f);
        var track = new StyleBoxFlat {
            BgColor = trackBg,
            BorderColor = trackEdge,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusBottomLeft = radius,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = vPad,
            ContentMarginBottom = vPad,
            ShadowSize = 0,
        };
        var ac = KitLibTheme.Accent;
        var fill = new StyleBoxFlat {
            BgColor = new Color(ac.R, ac.G, ac.B, 0.26f),
            BorderColor = Colors.Transparent,
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusBottomLeft = radius,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = vPad,
            ContentMarginBottom = vPad,
            ShadowSize = 0,
        };
        var fillHi = (StyleBoxFlat)fill.Duplicate();
        fillHi.BgColor = new Color(ac.R, ac.G, ac.B, 0.38f);
        s.AddThemeStyleboxOverride("slider", track);
        s.AddThemeStyleboxOverride("grabber_area", fill);
        s.AddThemeStyleboxOverride("grabber_area_highlight", fillHi);
        s.AddThemeColorOverride("grabber_area_color", new Color(ac.R, ac.G, ac.B, 0.55f));
        s.AddThemeColorOverride("grabber_area_highlight_color", new Color(ac.R, ac.G, ac.B, 0.78f));
        s.AddThemeConstantOverride("center_grabber", 1);
    }
    /// <summary>
    ///     Applies <see cref="ApplySliderStyle" /> and returns the same instance so call sites can write
    ///     <c>WithSliderStyle(new HSlider { ... })</c> in one expression.
    /// </summary>
    public static HSlider WithSliderStyle(HSlider s) {
        ApplySliderStyle(s);
        return s;
    }
    public static void ApplyToggle(CheckBox cb) {
        cb.AddThemeFontSizeOverride("font_size", 15);
        cb.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        cb.AddThemeColorOverride("font_pressed_color", KitLibTheme.Accent);
        cb.AddThemeColorOverride("font_hover_color", KitLibTheme.TextPrimary);
    }
    public static void ApplyOptionButton(OptionButton ob) {
        var n = RoundedField(false);
        var h = (StyleBoxFlat)n.Duplicate();
        var hc = h.BgColor;
        h.BgColor = new Color(hc.R, hc.G, hc.B, Mathf.Min(hc.A * 1.22f, 0.32f));
        var pr = (StyleBoxFlat)n.Duplicate();
        pr.BorderColor = FieldBorderColor(true);
        pr.BorderWidthLeft = pr.BorderWidthTop = pr.BorderWidthRight = pr.BorderWidthBottom = 2;
        ob.AddThemeStyleboxOverride("normal", n);
        ob.AddThemeStyleboxOverride("hover", h);
        ob.AddThemeStyleboxOverride("pressed", pr);
        ob.AddThemeStyleboxOverride("focus", RoundedField(true));
        ob.AddThemeFontSizeOverride("font_size", 14);
        ob.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        WireRoundedFieldFocusMotion(ob);
    }
    public static void ApplyLineEdit(LineEdit le) {
        var n = RoundedField(false);
        le.AddThemeStyleboxOverride("normal", n);
        le.AddThemeStyleboxOverride("read_only", n);
        le.AddThemeStyleboxOverride("focus", RoundedField(true));
        le.AddThemeFontSizeOverride("font_size", 14);
        le.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        le.AddThemeColorOverride("caret_color", FieldCaretColor());
        WireRoundedFieldFocusMotion(le);
    }
    public static void ApplyTextEdit(TextEdit te) {
        var n = RoundedField(false);
        te.AddThemeStyleboxOverride("normal", n);
        te.AddThemeStyleboxOverride("read_only", n);
        te.AddThemeStyleboxOverride("focus", RoundedField(true));
        te.AddThemeFontSizeOverride("font_size", 14);
        te.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        te.AddThemeColorOverride("caret_color", FieldCaretColor());
        WireRoundedFieldFocusMotion(te);
    }
    public static void ApplyMiniIconButton(Button b) {
        b.Flat = true;
        b.AddThemeFontSizeOverride("font_size", 18);
        b.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        b.AddThemeStyleboxOverride("normal", RoundedField(false));
        b.AddThemeStyleboxOverride("hover", RoundedField(true));
        b.AddThemeStyleboxOverride("pressed", RoundedField(true));
    }
    public static void ApplyAccentPillButton(Button b) {
        b.Flat = true;
        b.AddThemeFontSizeOverride("font_size", 15);
        b.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        var box = new StyleBoxFlat {
            BgColor = KitLibTheme.AccentAlpha,
            BorderColor = KitLibTheme.Accent,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusBottomLeft = 8,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        b.AddThemeStyleboxOverride("normal", box);
        b.AddThemeStyleboxOverride("hover", box);
        b.AddThemeStyleboxOverride("pressed", box);
    }
    public static Color RoundedFieldBorderColor(bool focused) => FieldBorderColor(focused);
    public static Color RoundedFieldCaretColor() => FieldCaretColor();
    public static StyleBoxFlat RoundedField(bool focused) {
        var borderW = focused ? 2 : 1;
        return new StyleBoxFlat {
            BgColor = FieldGlassFill(focused),
            BorderColor = FieldBorderColor(focused),
            BorderWidthLeft = borderW,
            BorderWidthRight = borderW,
            BorderWidthTop = borderW,
            BorderWidthBottom = borderW,
            CornerRadiusTopLeft = FieldCornerRadius,
            CornerRadiusTopRight = FieldCornerRadius,
            CornerRadiusBottomRight = FieldCornerRadius,
            CornerRadiusBottomLeft = FieldCornerRadius,
            ContentMarginLeft = 11,
            ContentMarginRight = 11,
            ContentMarginTop = 7,
            ContentMarginBottom = 7,
            ShadowSize = focused ? 4 : 0,
            ShadowOffset = new Vector2(0f, 1f),
            ShadowColor = FieldShadowColor(focused),
        };
    }
    /// <summary>Subtle focus/hover lift without accent colors (pairs with <see cref="RoundedField" />).</summary>
    public static void WireRoundedFieldFocusMotion(Control control) {
        const string wired = "dm_field_motion_wired";
        if (control.HasMeta(wired))
            return;
        control.SetMeta(wired, true);
        control.FocusEntered += () => RunFieldFocusTween(control, true);
        control.FocusExited += () => RunFieldFocusTween(control, false);
    }
    private const string MetaFieldTween = "dm_field_focus_tween";
    private static void RunFieldFocusTween(Control control, bool focused) {
        if (control.HasMeta(MetaFieldTween)) {
            var v = control.GetMeta(MetaFieldTween);
            if (v.VariantType == Variant.Type.Object && v.AsGodotObject() is Tween oldTw)
                oldTw.Kill();
            control.RemoveMeta(MetaFieldTween);
        }
        var tw = control.CreateTween();
        control.SetMeta(MetaFieldTween, tw);
        tw.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(control, "self_modulate", focused ? FieldFocusSelfModulate() : Colors.White, 0.14);
        tw.TweenCallback(Callable.From(() => {
            if (GodotObject.IsInstanceValid(control) && control.HasMeta(MetaFieldTween))
                control.RemoveMeta(MetaFieldTween);
        }));
    }
    private static Color FieldFocusSelfModulate() {
        var panel = KitLibTheme.PanelBg;
        var lum = panel.R * 0.299f + panel.G * 0.587f + panel.B * 0.114f;
        return lum > 0.52f
            ? new Color(0.97f, 0.97f, 0.99f, 1f)
            : new Color(1.04f, 1.04f, 1.055f, 1f);
    }
    private static Color FieldCaretColor() {
        var t = KitLibTheme.TextPrimary;
        return new Color(t.R, t.G, t.B, Mathf.Min(t.A * 1.15f, 1f));
    }
    private static Color FieldShadowColor(bool focused) {
        if (!focused)
            return Colors.Transparent;
        var panel = KitLibTheme.PanelBg;
        var lum = panel.R * 0.299f + panel.G * 0.587f + panel.B * 0.114f;
        return lum > 0.52f
            ? new Color(0f, 0f, 0f, 0.1f)
            : new Color(0f, 0f, 0f, 0.28f);
    }
    private static Color FieldGlassFill(bool focused) {
        var panel = KitLibTheme.PanelBg;
        var lum = panel.R * 0.299f + panel.G * 0.587f + panel.B * 0.114f;
        var a = focused ? 0.16f : 0.11f;
        if (lum > 0.52f)
            return new Color(0.10f, 0.11f, 0.14f, focused ? 0.12f : 0.085f);
        return new Color(1f, 1f, 1f, a);
    }
    private static Color FieldBorderColor(bool focused) {
        var b = KitLibTheme.PanelBorder;
        var baseBorder = new Color(b.R, b.G, b.B, Mathf.Clamp(b.A * 2f, 0.14f, 0.38f));
        if (!focused)
            return baseBorder;
        var t = KitLibTheme.TextPrimary;
        return new Color(
            Mathf.Lerp(baseBorder.R, t.R, 0.28f),
            Mathf.Lerp(baseBorder.G, t.G, 0.28f),
            Mathf.Lerp(baseBorder.B, t.B, 0.28f),
            Mathf.Clamp(baseBorder.A * 1.45f + 0.1f, 0.3f, 0.5f));
    }
}