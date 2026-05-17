using System;
using DevMode.Icons;
using Godot;

namespace DevMode.UI;

internal static partial class DevPanelUI {
    // ── Browser-panel factory (spliced to rail, same visual language as Cards/Relics) ─────────

    /// <summary>
    /// Creates a panel spliced to the left rail — flat left edge, rounded right corners,
    /// slide-in from left animation.
    /// <para>
    /// <paramref name="fixedWidth"/> &gt; 0 → panel is that many pixels wide (narrow list panels).
    /// <paramref name="fixedWidth"/> = 0 → panel expands to the right edge of the screen (full-width browsers).
    /// </para>
    /// </summary>
    public static PanelContainer CreateBrowserPanel(float fixedWidth = 0f) {
        var panel = new PanelContainer {
            Name = "BrowserPanel",
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorTop = 0.15f,
            AnchorBottom = 0.85f,
            OffsetTop = 0,
            OffsetBottom = 0
        };

        if (fixedWidth > 0f) {
            panel.AnchorLeft = 0; panel.AnchorRight = 0;
            panel.OffsetLeft = BrowserPanelLeft;
            panel.OffsetRight = BrowserPanelLeft + fixedWidth;
        }
        else {
            panel.AnchorLeft = 0; panel.AnchorRight = 1;
            panel.OffsetLeft = BrowserPanelLeft;
            panel.OffsetRight = -BrowserPanelRight;
        }

        var style = new StyleBoxFlat {
            BgColor = ColOverlayBg,
            CornerRadiusTopLeft = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusTopRight = BrowserRailRadius,
            CornerRadiusBottomRight = BrowserRailRadius,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 14,
            ContentMarginBottom = 16,
            BorderWidthLeft = 0,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthRight = 1,
            BorderColor = ColOverlayBorder,
            ShadowColor = new Color(0, 0, 0, 0.40f),
            ShadowSize = 20,
            ShadowOffset = new Vector2(20, 0)
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var content = new VBoxContainer { Name = "Content" };
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);

        return panel;
    }

    /// <summary>
    /// Same chrome as <see cref="CreateBrowserPanel"/> for a fixed pixel width, but horizontal
    /// offsets are <c>0 … width</c> (parent must already account for <see cref="BrowserPanelLeft"/>).
    /// Used for stacked browser columns inside one clip host (e.g. Save / Load + extension).
    /// </summary>
    /// <param name="joinFlushOnRight">
    /// When another column sits immediately to the right, square this panel's right vertical corners
    /// so the seam has no inner rounding (reads as one split surface).
    /// </param>
    public static PanelContainer CreateBrowserPanelInner(float fixedWidth, bool joinFlushOnRight = false) {
        var panel = new PanelContainer {
            Name = "BrowserPanel",
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorTop = 0.15f,
            AnchorBottom = 0.85f,
            OffsetTop = 0,
            OffsetBottom = 0,
            AnchorLeft = 0,
            AnchorRight = 0,
            OffsetLeft = 0,
            OffsetRight = fixedWidth,
        };

        int rr = joinFlushOnRight ? 0 : BrowserRailRadius;
        var style = new StyleBoxFlat {
            BgColor = ColOverlayBg,
            CornerRadiusTopLeft = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusTopRight = rr,
            CornerRadiusBottomRight = rr,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 14,
            ContentMarginBottom = 16,
            BorderWidthLeft = 0,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthRight = 1,
            BorderColor = ColOverlayBorder,
            ShadowColor = new Color(0, 0, 0, 0.40f),
            ShadowSize = 20,
            ShadowOffset = new Vector2(20, 0)
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var content = new VBoxContainer { Name = "Content" };
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);

        return panel;
    }

    /// <summary>
    /// Transparent full-area backdrop that closes the panel when clicking anywhere outside
    /// the rail (to the right of the rail, full height). Add this as the FIRST child of the
    /// panel root so the panel itself sits on top and receives clicks first.
    /// Only used for fixed-width browser panels; full-width panels fill the available area.
    /// </summary>
    public static ColorRect CreateBrowserBackdrop(Action onClose) {
        bool closed = false;
        void SafeClose() {
            if (closed) return;
            closed = true;
            onClose();
        }

        var backdrop = new ColorRect {
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 1,
            OffsetLeft = BrowserPanelLeft,
            OffsetRight = 0,
            OffsetTop = 0,
            OffsetBottom = 0
        };

        backdrop.GuiInput += e => {
            if (e is InputEventMouseButton { Pressed: true })
                SafeClose();
        };

        return backdrop;
    }

    // ── Shared overlay widget factories ──────────────────────────────────────

    /// <summary>Standard panel title label — matches relic / card browser headers.</summary>
    public static Label CreatePanelTitle(string text) {
        var lbl = new Label {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        lbl.AddThemeFontSizeOverride("font_size", 16);
        lbl.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        return lbl;
    }

    /// <summary>
    /// Standard search row with magnify icon — matches relic / card browser search bars.
    /// Returns the row container and the inner LineEdit.
    /// </summary>
    public static (HBoxContainer row, LineEdit input) CreateSearchRow(string placeholder) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        row.AddChild(new TextureRect {
            Texture = MdiIcon.Magnify.Texture(18, DevModeTheme.Subtle),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(22, 22),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        });

        var input = new LineEdit {
            PlaceholderText = placeholder,
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddChild(input);

        return (row, input);
    }

    /// <summary>
    /// Styled list-item button used in scroll lists (Powers, Potions, Events, etc.).
    /// Left-aligned text, subtle dark background, accent hover.
    /// </summary>
    public static Button CreateListItemButton(string text) {
        var btn = new Button {
            Text = text,
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 34),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
            ClipText = true
        };

        StyleBoxFlat MakeStyle(Color bg, Color border) => new() {
            BgColor = bg,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = border
        };

        var accent = DevModeTheme.Accent;
        var bgNormal = DevModeTheme.ButtonBgNormal;
        btn.AddThemeStyleboxOverride("normal", MakeStyle(bgNormal, new Color(bgNormal.R, bgNormal.G, bgNormal.B, bgNormal.A * 0.8f)));
        btn.AddThemeStyleboxOverride("hover", MakeStyle(DevModeTheme.ButtonBgHover, new Color(accent.R, accent.G, accent.B, 0.30f)));
        btn.AddThemeStyleboxOverride("pressed", MakeStyle(new Color(accent.R, accent.G, accent.B, 0.15f), new Color(accent.R, accent.G, accent.B, 0.50f)));
        btn.AddThemeStyleboxOverride("focus", MakeStyle(bgNormal, new Color(bgNormal.R, bgNormal.G, bgNormal.B, bgNormal.A * 0.8f)));

        btn.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        btn.AddThemeColorOverride("font_hover_color", DevModeTheme.TextPrimary);
        btn.AddThemeColorOverride("font_pressed_color", DevModeTheme.TextPrimary);
        btn.AddThemeFontSizeOverride("font_size", 13);

        return btn;
    }

    /// <summary>Pill-shaped segment filter chip — matches relic browser rarity chips.</summary>
    public static Button CreateFilterChip(string text, bool active = false) {
        var btn = new Button {
            Text = text,
            ToggleMode = true,
            ButtonPressed = active,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, 26)
        };

        StyleBoxFlat MakeStyle(Color bg) => new() {
            BgColor = bg,
            CornerRadiusTopLeft = 13,
            CornerRadiusTopRight = 13,
            CornerRadiusBottomLeft = 13,
            CornerRadiusBottomRight = 13,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };

        var accent = DevModeTheme.Accent;
        btn.AddThemeStyleboxOverride("normal", MakeStyle(DevModeTheme.ButtonBgNormal));
        btn.AddThemeStyleboxOverride("hover", MakeStyle(DevModeTheme.ButtonBgHover));
        btn.AddThemeStyleboxOverride("pressed", MakeStyle(DevModeTheme.AccentAlpha));
        btn.AddThemeStyleboxOverride("hover_pressed", MakeStyle(new Color(accent.R, accent.G, accent.B, 0.95f)));
        btn.AddThemeStyleboxOverride("focus", MakeStyle(DevModeTheme.ButtonBgNormal));

        btn.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
        btn.AddThemeColorOverride("font_hover_color", DevModeTheme.TextPrimary);
        btn.AddThemeColorOverride("font_pressed_color", DevModeTheme.TextPrimary);
        btn.AddThemeFontSizeOverride("font_size", 11);

        return btn;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static Button CreateRailIcon(MdiIcon icon, string tooltip) {
        var btn = new Button {
            CustomMinimumSize = new Vector2(IconBtnSize, IconBtnSize),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = tooltip,
            IconAlignment = HorizontalAlignment.Center,
            Icon = icon.Texture(20, ColIconNormal)
        };

        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        btn.AddThemeStyleboxOverride("normal", flat);
        btn.AddThemeStyleboxOverride("hover", flat);
        btn.AddThemeStyleboxOverride("pressed", flat);
        btn.AddThemeStyleboxOverride("focus", flat);

        return btn;
    }

    private static Button CreateToggleButton(string text) {
        return new Button {
            Text = text,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
    }

    private static void ApplyDisabledStyle(Button btn, int cornerFlags) {
        var s = new StyleBoxFlat {
            BgColor = DevModeTheme.ButtonBgNormal,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = DevModeTheme.Separator,
            CornerRadiusTopLeft = (cornerFlags & 1) != 0 ? 6 : 0,
            CornerRadiusBottomLeft = (cornerFlags & 1) != 0 ? 6 : 0,
            CornerRadiusTopRight = (cornerFlags & 2) != 0 ? 6 : 0,
            CornerRadiusBottomRight = (cornerFlags & 2) != 0 ? 6 : 0
        };
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            btn.AddThemeStyleboxOverride(state, s);
        btn.AddThemeColorOverride("font_disabled_color", DevModeTheme.Subtle);
    }

    private static void ApplyToggleStyle(Button btn, bool active, int cornerFlags) {
        var accent = DevModeTheme.Accent;
        var s = new StyleBoxFlat {
            BgColor = active ? new Color(accent.R, accent.G, accent.B, 0.25f) : DevModeTheme.ButtonBgNormal,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = active ? new Color(accent.R, accent.G, accent.B, 0.6f) : DevModeTheme.Separator,
            CornerRadiusTopLeft = (cornerFlags & 1) != 0 ? 6 : 0,
            CornerRadiusBottomLeft = (cornerFlags & 1) != 0 ? 6 : 0,
            CornerRadiusTopRight = (cornerFlags & 2) != 0 ? 6 : 0,
            CornerRadiusBottomRight = (cornerFlags & 2) != 0 ? 6 : 0
        };
        btn.AddThemeStyleboxOverride("normal", s);
        btn.AddThemeStyleboxOverride("hover", s);
        btn.AddThemeStyleboxOverride("pressed", s);
        btn.AddThemeStyleboxOverride("focus", s);
        btn.AddThemeColorOverride("font_color", active ? DevModeTheme.Accent : DevModeTheme.TextPrimary);
    }

    private static void ApplySmallButtonStyle(Button btn) {
        var normal = new StyleBoxFlat {
            BgColor = DevModeTheme.ButtonBgNormal,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        var hover = new StyleBoxFlat {
            BgColor = DevModeTheme.ButtonBgHover,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", normal);
    }

    private static void ApplySpinBoxTheme(SpinBox spinBox) {
        var lineEdit = spinBox.GetLineEdit();
        lineEdit.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        var bg = new StyleBoxFlat {
            BgColor = DevModeTheme.ButtonBgNormal,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        lineEdit.AddThemeStyleboxOverride("normal", bg);
        lineEdit.AddThemeStyleboxOverride("focus", bg);
        lineEdit.AddThemeStyleboxOverride("read_only", bg);
    }

    private static Button CreateButton(string text, Action action, MdiIcon? icon = null) {
        var btn = CreatePlainButton(text, icon);
        btn.Pressed += action;
        return btn;
    }

    private static Button CreatePlainButton(string text, MdiIcon? icon = null) {
        var btn = new Button {
            Text = text,
            CustomMinimumSize = new Vector2(0, 36),
            FocusMode = Control.FocusModeEnum.None
        };
        if (icon is { } ic) {
            btn.Icon = ic.Texture(16);
            btn.IconAlignment = HorizontalAlignment.Left;
            btn.Alignment = HorizontalAlignment.Left;
        }
        var normal = new StyleBoxFlat {
            BgColor = DevModeTheme.ButtonBgNormal,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
        var hover = new StyleBoxFlat {
            BgColor = DevModeTheme.ButtonBgHover,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", normal);
        btn.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        btn.AddThemeColorOverride("font_hover_color", DevModeTheme.TextPrimary);
        btn.AddThemeColorOverride("font_pressed_color", DevModeTheme.TextPrimary);
        btn.AddThemeFontSizeOverride("font_size", 13);
        return btn;
    }

    private static Button CreateOverlayButton(string text, MdiIcon icon) {
        var accent = DevModeTheme.Accent;
        var btn = new Button {
            Text = text,
            CustomMinimumSize = new Vector2(200, 48),
            FocusMode = Control.FocusModeEnum.None,
            Icon = icon.Texture(20, DevModeTheme.TextPrimary),
            IconAlignment = HorizontalAlignment.Left,
            Alignment = HorizontalAlignment.Center
        };
        var style = new StyleBoxFlat {
            BgColor = DevModeTheme.ButtonBgNormal,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        var hoverStyle = new StyleBoxFlat {
            BgColor = new Color(accent.R, accent.G, accent.B, 0.18f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);
        btn.AddThemeStyleboxOverride("pressed", hoverStyle);
        btn.AddThemeStyleboxOverride("focus", style);
        btn.AddThemeFontSizeOverride("font_size", 14);
        btn.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        return btn;
    }

    public static HSeparator CreateOverlaySeparator() {
        var sep = new HSeparator();
        sep.AddThemeStyleboxOverride("separator", new StyleBoxFlat {
            BgColor = ColSeparator,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
            ContentMarginLeft = 0,
            ContentMarginRight = 0
        });
        sep.AddThemeConstantOverride("separation", 4);
        return sep;
    }

    public static Control CreateSectionHeader(string text) {
        var container = new HBoxContainer();
        container.AddThemeConstantOverride("separation", 8);

        var line1 = new HSeparator {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        line1.AddThemeStyleboxOverride("separator", new StyleBoxFlat {
            BgColor = ColSeparator,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
            ContentMarginLeft = 0,
            ContentMarginRight = 0
        });
        line1.AddThemeConstantOverride("separation", 1);

        var label = new Label {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", ColSectionText);

        var line2 = new HSeparator {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        line2.AddThemeStyleboxOverride("separator", new StyleBoxFlat {
            BgColor = ColSeparator,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
            ContentMarginLeft = 0,
            ContentMarginRight = 0
        });
        line2.AddThemeConstantOverride("separation", 1);

        container.AddChild(line1);
        container.AddChild(label);
        container.AddChild(line2);
        return container;
    }

    private static Control CreateCheatToggle(string label, string? tooltip, Func<bool> getter, Action<bool> setter) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        row.CustomMinimumSize = new Vector2(0, 30);

        var lbl = new Label {
            Text = label,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true
        };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        lbl.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        if (tooltip != null) lbl.TooltipText = tooltip;
        row.AddChild(lbl);

        string onText = I18N.T("cheat.off", "Off");
        string offText = I18N.T("cheat.on", "On");

        var offBtn = new Button { Text = onText, CustomMinimumSize = new Vector2(36, 26), FocusMode = Control.FocusModeEnum.None };
        var onBtn = new Button { Text = offText, CustomMinimumSize = new Vector2(36, 26), FocusMode = Control.FocusModeEnum.None };

        void Refresh() {
            bool active = getter();
            ApplyToggleStyle(offBtn, !active, 1);
            ApplyToggleStyle(onBtn, active, 2);
        }

        offBtn.Pressed += () => { setter(false); Refresh(); };
        onBtn.Pressed += () => { setter(true); Refresh(); };

        row.AddChild(offBtn);
        row.AddChild(onBtn);
        Refresh();

        row.VisibilityChanged += () => {
            if (row.Visible) Refresh();
        };

        return row;
    }

    private static Control CreateCheatSlider(string label, string? tooltip, float min, float max, float step,
        Func<float> getter, Action<float> setter) {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var lbl = new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        lbl.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        if (tooltip != null) lbl.TooltipText = tooltip;
        row.AddChild(lbl);

        var valLabel = new Label { Text = getter().ToString("0.#"), CustomMinimumSize = new Vector2(28, 0) };
        valLabel.AddThemeFontSizeOverride("font_size", 12);
        valLabel.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        valLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(valLabel);

        col.AddChild(row);

        var slider = new HSlider {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = getter(),
            CustomMinimumSize = new Vector2(0, 20),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        slider.ValueChanged += v => {
            setter((float)v);
            valLabel.Text = ((float)v).ToString("0.#");
        };
        col.AddChild(slider);

        col.VisibilityChanged += () => {
            if (col.Visible) {
                slider.Value = getter();
                valLabel.Text = getter().ToString("0.#");
            }
        };

        return col;
    }

    private static Control CreateCheatNumberEdit(string label, int min, int max, Func<int> getter, Action<int> setter) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        row.CustomMinimumSize = new Vector2(0, 30);

        var lbl = new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        lbl.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        row.AddChild(lbl);

        var minusBtn = new Button { CustomMinimumSize = new Vector2(26, 26), FocusMode = Control.FocusModeEnum.None };
        minusBtn.Icon = MdiIcon.Minus.Texture(14, DevModeTheme.TextPrimary);
        ApplySmallButtonStyle(minusBtn);
        row.AddChild(minusBtn);

        var spinBox = new SpinBox {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            Value = getter(),
            CustomMinimumSize = new Vector2(50, 26),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            Alignment = HorizontalAlignment.Center
        };
        ApplySpinBoxTheme(spinBox);
        row.AddChild(spinBox);

        var plusBtn = new Button { CustomMinimumSize = new Vector2(26, 26), FocusMode = Control.FocusModeEnum.None };
        plusBtn.Icon = MdiIcon.Plus.Texture(14, DevModeTheme.TextPrimary);
        ApplySmallButtonStyle(plusBtn);
        row.AddChild(plusBtn);

        var applyBtn = new Button { CustomMinimumSize = new Vector2(26, 26), FocusMode = Control.FocusModeEnum.None };
        applyBtn.Icon = MdiIcon.Check.Texture(14, DevModeTheme.TextPrimary);
        var applyStyle = new StyleBoxFlat {
            BgColor = new Color(0.2f, 0.5f, 0.4f, 0.9f),
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
        applyBtn.AddThemeStyleboxOverride("normal", applyStyle);
        applyBtn.AddThemeStyleboxOverride("hover", applyStyle);
        applyBtn.AddThemeStyleboxOverride("pressed", applyStyle);
        row.AddChild(applyBtn);

        minusBtn.Pressed += () => spinBox.Value = Math.Max(min, spinBox.Value - 1);
        plusBtn.Pressed += () => spinBox.Value = Math.Min(max, spinBox.Value + 1);
        applyBtn.Pressed += () => setter((int)spinBox.Value);

        row.VisibilityChanged += () => {
            if (row.Visible) spinBox.Value = getter();
        };

        return row;
    }

    private static Control CreateRuntimeToggle(string label, string? tooltip, Func<bool> getter, Action<bool> setter) {
        return CreateCheatToggle(label, tooltip, getter, setter);
    }

    private static Control CreateStatLockRow(string label, int min, int max,
        Func<bool> lockGetter, Action<bool> lockSetter,
        Func<int> valueGetter, Action<int> valueSetter,
        Func<int>? liveValueGetter = null) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        row.CustomMinimumSize = new Vector2(0, 30);

        var check = new CheckBox { Text = label, ButtonPressed = lockGetter() };
        check.AddThemeFontSizeOverride("font_size", 12);
        check.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        check.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        check.ClipText = true;
        row.AddChild(check);

        var spinBox = new SpinBox {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            Value = valueGetter(),
            CustomMinimumSize = new Vector2(70, 26),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            Alignment = HorizontalAlignment.Center
        };
        ApplySpinBoxTheme(spinBox);
        row.AddChild(spinBox);

        check.Toggled += v => {
            if (v && liveValueGetter != null) {
                int live = liveValueGetter();
                valueSetter(live);
                spinBox.Value = live;
            }
            lockSetter(v);
        };
        spinBox.ValueChanged += v => valueSetter((int)v);

        row.VisibilityChanged += () => {
            if (row.Visible) {
                check.ButtonPressed = lockGetter();
                if (!lockGetter() && liveValueGetter != null)
                    spinBox.Value = liveValueGetter();
                else
                    spinBox.Value = valueGetter();
            }
        };

        return row;
    }
}
