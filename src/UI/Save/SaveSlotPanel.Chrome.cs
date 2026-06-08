using System;
using KitLib;
using Godot;

namespace KitLib.UI;

/// <summary>Static theming and small widget factories for <see cref="SaveSlotPanel"/>.</summary>
internal sealed partial class SaveSlotPanel : Control {
    private static Label DetailWrapLabel(int fontSize, Color color) {
        var l = new Label {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private static Label MutedLineLabel(int fontSize) {
        var l = new Label();
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        return l;
    }

    private static Control BuildMiniHpBar(int hp, int maxHp) {
        float ratio = maxHp > 0 ? Mathf.Clamp((float)hp / maxHp, 0f, 1f) : 0f;

        var container = new Control { CustomMinimumSize = new Vector2(0, 4) };

        var bg = new ColorRect {
            Color = KitLibTheme.PanelBg.Lerp(KitLibTheme.TextPrimary, 0.12f),
            AnchorRight = 1f,
            AnchorBottom = 1f,
        };
        container.AddChild(bg);

        var fill = new ColorRect {
            Color = HpColor(hp, maxHp),
            AnchorRight = ratio,
            AnchorBottom = 1f,
        };
        container.AddChild(fill);

        return container;
    }

    private static StyleBoxFlat MakeSlotCardStyle(bool selected, bool hover = false) {
        Color bg;
        Color border;
        if (selected) {
            bg = KitLibTheme.AccentAlpha with { A = 0.15f };
            border = KitLibTheme.Accent;
        }
        else if (hover) {
            bg = KitLibTheme.PanelBg.Lerp(KitLibTheme.Accent, 0.10f);
            border = KitLibTheme.PanelBorder.Lerp(KitLibTheme.Accent, 0.40f);
        }
        else {
            bg = KitLibTheme.PanelBg.Lerp(KitLibTheme.TextPrimary, 0.06f);
            border = KitLibTheme.PanelBorder;
        }

        return new StyleBoxFlat {
            BgColor = bg,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            BorderWidthLeft = selected ? 3 : 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = border,
        };
    }

    private static Label SectionHeader(string text) {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 13);
        lbl.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        return lbl;
    }

    private static Label MakeBadgeLabel() {
        var lbl = new Label();
        lbl.AddThemeFontSizeOverride("font_size", 13);
        lbl.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        return lbl;
    }

    private static PanelContainer MakeBadge(Label label, Color bgColor) {
        var badge = new PanelContainer();
        var style = new StyleBoxFlat {
            BgColor = bgColor,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
        };
        badge.AddThemeStyleboxOverride("panel", style);
        badge.AddChild(label);
        return badge;
    }

    private static HSeparator MakeThinSep() {
        var sep = new HSeparator();
        sep.AddThemeColorOverride("separator", KitLibTheme.Separator);
        return sep;
    }

    internal static Color OverlayScrimColor() {
        var baseCol = KitLibTheme.PanelBg.Lerp(Colors.Black, 0.52f);
        return baseCol with { A = 0.78f };
    }

    private static Color BadgeTint(Color hue, float alpha) => hue with { A = alpha };

    private static StyleBoxFlat MakeButtonBox(Color bg, Color border, int radius = 6, int marginH = 12, int marginV = 5) => new() {
        BgColor = bg,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = marginH,
        ContentMarginRight = marginH,
        ContentMarginTop = marginV,
        ContentMarginBottom = marginV,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderColor = border,
    };

    private static void ApplySecondaryButton(Button btn) {
        var bgN = KitLibTheme.ButtonBgNormal;
        var borderN = new Color(bgN.R, bgN.G, bgN.B, Mathf.Max(bgN.A, 0.08f));
        var accent = KitLibTheme.Accent;
        btn.AddThemeStyleboxOverride("normal", MakeButtonBox(bgN, borderN));
        btn.AddThemeStyleboxOverride("hover", MakeButtonBox(KitLibTheme.ButtonBgHover, new Color(accent.R, accent.G, accent.B, 0.28f)));
        btn.AddThemeStyleboxOverride("pressed", MakeButtonBox(new Color(accent.R, accent.G, accent.B, 0.14f), new Color(accent.R, accent.G, accent.B, 0.45f)));
        btn.AddThemeStyleboxOverride("disabled", MakeButtonBox(bgN with { A = bgN.A * 0.45f }, borderN with { A = borderN.A * 0.45f }));
        btn.AddThemeStyleboxOverride("focus", MakeButtonBox(bgN, borderN));
        btn.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        btn.AddThemeColorOverride("font_hover_color", KitLibTheme.TextPrimary);
        btn.AddThemeColorOverride("font_pressed_color", KitLibTheme.TextPrimary);
        btn.AddThemeColorOverride("font_disabled_color", KitLibTheme.Subtle);
    }

    private static Color TextOnAccentBackground() {
        var a = KitLibTheme.Accent;
        float lum = 0.299f * a.R + 0.587f * a.G + 0.114f * a.B;
        return lum > 0.55f ? new Color(0.12f, 0.10f, 0.08f, 1f) : Colors.White;
    }

    private static void ApplyPrimaryButton(Button btn) {
        var accent = KitLibTheme.Accent;
        var bgN = accent with { A = 0.38f };
        var bgH = accent with { A = 0.52f };
        var bgP = accent with { A = 0.30f };
        var border = new Color(accent.R, accent.G, accent.B, 0.55f);
        var fg = TextOnAccentBackground();
        btn.AddThemeStyleboxOverride("normal", MakeButtonBox(bgN, border));
        btn.AddThemeStyleboxOverride("hover", MakeButtonBox(bgH, border with { A = 0.75f }));
        btn.AddThemeStyleboxOverride("pressed", MakeButtonBox(bgP, border));
        btn.AddThemeStyleboxOverride("disabled", MakeButtonBox(bgN with { A = bgN.A * 0.4f }, border with { A = border.A * 0.4f }));
        btn.AddThemeStyleboxOverride("focus", MakeButtonBox(bgN, border));
        btn.AddThemeColorOverride("font_color", fg);
        btn.AddThemeColorOverride("font_hover_color", fg);
        btn.AddThemeColorOverride("font_pressed_color", fg);
        btn.AddThemeColorOverride("font_disabled_color", KitLibTheme.Subtle);
    }

    private static void ApplyDangerButton(Button btn) {
        var danger = KitLibTheme.RarityCurse;
        var bgN = KitLibTheme.ButtonBgNormal;
        var borderN = new Color(bgN.R, bgN.G, bgN.B, Mathf.Max(bgN.A, 0.08f));
        btn.AddThemeStyleboxOverride("normal", MakeButtonBox(bgN, new Color(danger.R, danger.G, danger.B, 0.35f)));
        btn.AddThemeStyleboxOverride("hover", MakeButtonBox(new Color(danger.R, danger.G, danger.B, 0.14f), new Color(danger.R, danger.G, danger.B, 0.55f)));
        btn.AddThemeStyleboxOverride("pressed", MakeButtonBox(new Color(danger.R, danger.G, danger.B, 0.22f), danger with { A = 0.65f }));
        btn.AddThemeStyleboxOverride("focus", MakeButtonBox(bgN, new Color(danger.R, danger.G, danger.B, 0.35f)));
        btn.AddThemeColorOverride("font_color", danger);
        btn.AddThemeColorOverride("font_hover_color", danger);
        btn.AddThemeColorOverride("font_pressed_color", danger);
    }

    private static void ApplyThemedLineEdit(LineEdit edit) {
        StyleBoxFlat FieldStyle(bool focused) => new() {
            BgColor = KitLibTheme.ButtonBgNormal,
            BorderColor = focused ? KitLibTheme.Accent : KitLibTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
        edit.AddThemeStyleboxOverride("normal", FieldStyle(false));
        edit.AddThemeStyleboxOverride("focus", FieldStyle(true));
        edit.AddThemeStyleboxOverride("read_only", FieldStyle(false));
        edit.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        edit.AddThemeColorOverride("font_placeholder_color", KitLibTheme.Subtle);
        edit.AddThemeColorOverride("caret_color", KitLibTheme.Accent);
        edit.AddThemeColorOverride("selection_color", KitLibTheme.AccentAlpha with { A = 0.35f });
    }

    private static Color HpColor(int hp, int maxHp) {
        if (maxHp <= 0) return KitLibTheme.Subtle;
        float ratio = (float)hp / maxHp;
        var accent = KitLibTheme.Accent;
        var mid = KitLibTheme.TextSecondary;
        if (ratio > 0.6f) return accent;
        if (ratio > 0.3f) return mid.Lerp(accent, (ratio - 0.3f) / 0.3f);
        return KitLibTheme.RarityCurse.Lerp(mid, ratio / 0.3f);
    }

    private static StyleBoxFlat MakePanel(
        float tl = 10, float tr = 10, float br = 10, float bl = 10, Color? color = null) {
        return new StyleBoxFlat {
            BgColor = color ?? KitLibTheme.PanelBg,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 16,
            ContentMarginBottom = 16,
            CornerRadiusTopLeft = (int)tl,
            CornerRadiusTopRight = (int)tr,
            CornerRadiusBottomRight = (int)br,
            CornerRadiusBottomLeft = (int)bl,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = KitLibTheme.PanelBorder,
        };
    }

    /// <summary>Panel chrome without outer stroke — slot dialog reads cleaner inside fullscreen / browser.</summary>
    internal static StyleBoxFlat MakePanelFlat(
        float tl = 10, float tr = 10, float br = 10, float bl = 10, Color? color = null) {
        return new StyleBoxFlat {
            BgColor = color ?? KitLibTheme.PanelBg,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 16,
            ContentMarginBottom = 16,
            CornerRadiusTopLeft = (int)tl,
            CornerRadiusTopRight = (int)tr,
            CornerRadiusBottomRight = (int)br,
            CornerRadiusBottomLeft = (int)bl,
            BorderWidthBottom = 0,
            BorderWidthTop = 0,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
        };
    }
}
