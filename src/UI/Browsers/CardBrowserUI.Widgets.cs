using System;
using System.Collections.Generic;
using Godot;

namespace KitLib.UI;

internal static partial class CardBrowserUI {
    // ── Nav tab colors ──

    private static Color ColNavActive => KitLibTheme.Accent;
    private static Color ColNavInactive => KitLibTheme.Subtle;
    private static Color ColNavHover => KitLibTheme.TextPrimary;
    private static Color ColNavAccent => KitLibTheme.AccentAlpha;

    private static Button CreateNavTab(string text, bool active) {
        var btn = new Button {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, 32)
        };

        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 4,
            ContentMarginBottom = 6
        };

        btn.AddThemeStyleboxOverride("normal", flat);
        btn.AddThemeStyleboxOverride("hover", flat);
        btn.AddThemeStyleboxOverride("pressed", flat);
        btn.AddThemeStyleboxOverride("focus", flat);

        btn.AddThemeColorOverride("font_color", active ? ColNavActive : ColNavInactive);
        btn.AddThemeColorOverride("font_hover_color", active ? ColNavActive : ColNavHover);
        btn.AddThemeColorOverride("font_pressed_color", ColNavActive);
        btn.AddThemeFontSizeOverride("font_size", 13);

        return btn;
    }

    // ── Sort toggle button ──

    private static Color ColSortBg => KitLibTheme.ButtonBgNormal;
    private static Color ColSortHover => KitLibTheme.ButtonBgHover;
    private static Color ColSortPressed => KitLibTheme.AccentAlpha;

    private static Button CreateSortToggleButton(string text) {
        var btn = new Button {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, 28)
        };

        StyleBoxFlat MakeSortStyle(Color bg) => new() {
            BgColor = bg,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };

        btn.AddThemeStyleboxOverride("normal", MakeSortStyle(ColSortBg));
        btn.AddThemeStyleboxOverride("hover", MakeSortStyle(ColSortHover));
        btn.AddThemeStyleboxOverride("pressed", MakeSortStyle(ColSortPressed));
        btn.AddThemeStyleboxOverride("focus", MakeSortStyle(ColSortBg));

        btn.AddThemeColorOverride("font_color", ColNavInactive);
        btn.AddThemeColorOverride("font_hover_color", ColNavHover);
        btn.AddThemeColorOverride("font_pressed_color", ColNavActive);
        btn.AddThemeFontSizeOverride("font_size", 12);

        return btn;
    }

    // ── Filter chip (toggle button) ──

    private static Color ColChipOff => KitLibTheme.ButtonBgNormal;
    private static Color ColChipHover => KitLibTheme.ButtonBgHover;
    private static readonly Color ColChipOn = new(0.25f, 0.40f, 0.65f, 0.90f);
    private static readonly Color ColChipOnHover = new(0.30f, 0.48f, 0.75f, 0.95f);
    private static readonly Color ColChipExclude = new(0.65f, 0.22f, 0.22f, 0.92f);
    private static readonly Color ColChipExcludeHover = new(0.75f, 0.28f, 0.28f, 0.95f);

    private static void ToggleSet<T>(HashSet<T> set, T value, bool on) {
        if (on) set.Add(value);
        else set.Remove(value);
    }

    private enum FilterChipMode { Off, Include, Exclude }

    private static StyleBoxFlat MakeChipStyleBox(Color bg) => new() {
        BgColor = bg,
        CornerRadiusTopLeft = 12,
        CornerRadiusTopRight = 12,
        CornerRadiusBottomLeft = 12,
        CornerRadiusBottomRight = 12,
        ContentMarginLeft = 8,
        ContentMarginRight = 8,
        ContentMarginTop = 1,
        ContentMarginBottom = 1
    };

    private static Button CreateFilterChip(string text, bool buttonPressed = false) {
        var btn = new Button {
            Text = text,
            ToggleMode = true,
            ButtonPressed = buttonPressed,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, 24)
        };

        ApplyFilterChipVisual(btn, buttonPressed ? FilterChipMode.Include : FilterChipMode.Off);

        btn.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        btn.AddThemeColorOverride("font_hover_color", KitLibTheme.TextPrimary);
        btn.AddThemeColorOverride("font_pressed_color", KitLibTheme.TextPrimary);
        btn.AddThemeFontSizeOverride("font_size", 11);

        return btn;
    }

    private static void ApplyFilterChipVisual(Button chip, FilterChipMode mode) {
        switch (mode) {
            case FilterChipMode.Include:
                chip.AddThemeStyleboxOverride("normal", MakeChipStyleBox(ColChipOn));
                chip.AddThemeStyleboxOverride("hover", MakeChipStyleBox(ColChipOnHover));
                chip.AddThemeStyleboxOverride("pressed", MakeChipStyleBox(ColChipOn));
                chip.AddThemeStyleboxOverride("hover_pressed", MakeChipStyleBox(ColChipOnHover));
                chip.AddThemeStyleboxOverride("focus", MakeChipStyleBox(ColChipOn));
                break;
            case FilterChipMode.Exclude:
                chip.AddThemeStyleboxOverride("normal", MakeChipStyleBox(ColChipExclude));
                chip.AddThemeStyleboxOverride("hover", MakeChipStyleBox(ColChipExcludeHover));
                chip.AddThemeStyleboxOverride("pressed", MakeChipStyleBox(ColChipExclude));
                chip.AddThemeStyleboxOverride("hover_pressed", MakeChipStyleBox(ColChipExcludeHover));
                chip.AddThemeStyleboxOverride("focus", MakeChipStyleBox(ColChipExclude));
                break;
            default:
                chip.AddThemeStyleboxOverride("normal", MakeChipStyleBox(ColChipOff));
                chip.AddThemeStyleboxOverride("hover", MakeChipStyleBox(ColChipHover));
                chip.AddThemeStyleboxOverride("pressed", MakeChipStyleBox(ColChipOn));
                chip.AddThemeStyleboxOverride("hover_pressed", MakeChipStyleBox(ColChipOnHover));
                chip.AddThemeStyleboxOverride("focus", MakeChipStyleBox(ColChipOff));
                break;
        }
    }

    private static FilterChipMode ResolveFilterChipMode(bool included, bool excluded) {
        if (included) return FilterChipMode.Include;
        if (excluded) return FilterChipMode.Exclude;
        return FilterChipMode.Off;
    }

    private static void WireTriStateFilterChip(
        Button chip,
        Action<bool> setInclude,
        Action<bool> setExclude,
        FilterChipMode initialMode,
        Action refresh) {
        var mode = initialMode;
        var suppressToggle = false;

        void SetMode(FilterChipMode next) {
            mode = next;
            suppressToggle = true;
            switch (next) {
                case FilterChipMode.Include:
                    chip.SetPressedNoSignal(true);
                    setInclude(true);
                    setExclude(false);
                    break;
                case FilterChipMode.Exclude:
                    chip.SetPressedNoSignal(false);
                    setInclude(false);
                    setExclude(true);
                    break;
                default:
                    chip.SetPressedNoSignal(false);
                    setInclude(false);
                    setExclude(false);
                    break;
            }
            ApplyFilterChipVisual(chip, mode);
            suppressToggle = false;
        }

        chip.SetPressedNoSignal(mode == FilterChipMode.Include);
        SetMode(mode);

        chip.Toggled += on => {
            if (suppressToggle) return;
            SetMode(on ? FilterChipMode.Include : FilterChipMode.Off);
            refresh();
        };

        chip.GuiInput += evt => {
            if (evt is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
                return;
            SetMode(mode == FilterChipMode.Exclude ? FilterChipMode.Off : FilterChipMode.Exclude);
            refresh();
            chip.AcceptEvent();
        };
    }

    // ── Panel colors ──

    private static Color ColPanelBg => KitLibTheme.PanelBg;
    private static Color ColPanelBorder => KitLibTheme.PanelBorder;

    private static PanelContainer CreateBrowserPanel() {
        var panel = new PanelContainer {
            Name = "BrowserPanel",
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 0,
            AnchorRight = 1,
            OffsetLeft = PanelLeft,
            OffsetRight = -PanelRight,
            AnchorTop = 0.15f,
            AnchorBottom = 0.85f,
            OffsetTop = 0,
            OffsetBottom = 0
        };

        var style = new StyleBoxFlat {
            BgColor = ColPanelBg,
            CornerRadiusTopLeft = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusTopRight = RailRadius,
            CornerRadiusBottomRight = RailRadius,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 12,
            ContentMarginBottom = 16,
            BorderWidthLeft = 0,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthRight = 1,
            BorderColor = ColPanelBorder,
            ShadowColor = new Color(0, 0, 0, 0.40f),
            ShadowSize = 20,
            ShadowOffset = new Vector2(20, 0)
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var content = new VBoxContainer { Name = "Content" };
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);

        return panel;
    }
}
