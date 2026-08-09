using System;
using System.Collections.Generic;
using Godot;

namespace KitLib.UI;

internal static partial class RelicBrowserUI {
    // ── Nav / accent colours ──

    private static Color ColNavActive => KitLibTheme.Accent;
    private static Color ColNavInactive => KitLibTheme.Subtle;
    private static Color ColNavHover => KitLibTheme.TextPrimary;
    private static Color ColNavAccent => KitLibTheme.AccentAlpha;

    // ── Panel / border colours ──

    private static Color ColSubtle => KitLibTheme.Subtle;

    // (tile frame colours defined in RelicBrowserUI.Grid.cs)

    // ── Sort button colours ──

    private static Color ColSortBg => KitLibTheme.ButtonBgNormal;
    private static Color ColSortHover => KitLibTheme.ButtonBgHover;
    private static Color ColSortPressed => KitLibTheme.AccentAlpha;

    // ── Segment filter colours (pill-shaped toggle) ──

    private static Color ColSegOff => KitLibTheme.ButtonBgNormal;
    private static Color ColSegHover => KitLibTheme.ButtonBgHover;
    private static readonly Color ColSegOn = new(0.25f, 0.40f, 0.65f, 0.90f);
    private static readonly Color ColSegOnHover = new(0.30f, 0.48f, 0.75f, 0.95f);

    // ── Navigation tab ──

    private static Button CreateNavTab(string text, bool active) {
        var btn = new Button {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, 32)
        };

        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
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

    private static Button CreateSortButton(string text) {
        var btn = new Button {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, 28)
        };

        StyleBoxFlat MakeStyle(Color bg) => new() {
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

        btn.AddThemeStyleboxOverride("normal", MakeStyle(ColSortBg));
        btn.AddThemeStyleboxOverride("hover", MakeStyle(ColSortHover));
        btn.AddThemeStyleboxOverride("pressed", MakeStyle(ColSortPressed));
        btn.AddThemeStyleboxOverride("focus", MakeStyle(ColSortBg));

        btn.AddThemeColorOverride("font_color", ColNavInactive);
        btn.AddThemeColorOverride("font_hover_color", ColNavHover);
        btn.AddThemeColorOverride("font_pressed_color", ColNavActive);
        btn.AddThemeFontSizeOverride("font_size", 12);

        return btn;
    }

    // ── Tier segment pill (toggle button) ──

    private static Button CreateSegmentChip(string text) {
        var btn = new Button {
            Text = text,
            ToggleMode = true,
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
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };

        btn.AddThemeStyleboxOverride("normal", MakeStyle(ColSegOff));
        btn.AddThemeStyleboxOverride("hover", MakeStyle(ColSegHover));
        btn.AddThemeStyleboxOverride("pressed", MakeStyle(ColSegOn));
        btn.AddThemeStyleboxOverride("hover_pressed", MakeStyle(ColSegOnHover));
        btn.AddThemeStyleboxOverride("focus", MakeStyle(ColSegOff));

        btn.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        btn.AddThemeColorOverride("font_hover_color", KitLibTheme.TextPrimary);
        btn.AddThemeColorOverride("font_pressed_color", KitLibTheme.TextPrimary);
        btn.AddThemeFontSizeOverride("font_size", 11);

        return btn;
    }

    // ── Action button (shared with right panel) ──

    private static Button CreateActionButton(string text, Color bgColor) {
        var btn = new Button {
            Text = text,
            CustomMinimumSize = new Vector2(0, 36),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var style = new StyleBoxFlat {
            BgColor = bgColor,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 5,
            ContentMarginBottom = 5
        };
        var hover = new StyleBoxFlat {
            BgColor = bgColor.Lightened(0.15f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 5,
            ContentMarginBottom = 5
        };
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", style);
        btn.AddThemeFontSizeOverride("font_size", 13);
        return btn;
    }

    private static void ToggleSet<T>(HashSet<T> set, T value, bool on) {
        if (on) set.Add(value);
        else set.Remove(value);
    }
}
