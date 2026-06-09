using System;
using KitLib.ModPanel.Icons;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
namespace KitLib.UI;
public static partial class ModPanelUI {
    private static MegaRichTextLabel CreateInlineDescription(string text) {
        var label = CreateHeaderLabel(text, 16, HorizontalAlignment.Left, ModPanelUiPalette.RichTextSecondary);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return label;
    }
    internal static void ApplySidebarModGroupInnerRowStyle(StyleBoxFlat box, bool selected, bool pressed,
        bool focused = false) {
        var a = ModPanelUiMetrics.SidebarModListSubtleAlpha;
        var stripWhite = new Color(1f, 1f, 1f, a);
        if (pressed && selected)
            stripWhite = new Color(1f, 1f, 1f, a * 0.75f);
        var dimLine = new Color(1f, 1f, 1f, a);
        Color modGroupBg;
        Color bottomColor;
        int bottomBorderW;
        if (selected) {
            modGroupBg = stripWhite;
            bottomColor = Colors.Transparent;
            bottomBorderW = 0;
        }
        else if (focused) {
            modGroupBg = new Color(1f, 1f, 1f, a * 0.55f);
            bottomColor = Colors.Transparent;
            bottomBorderW = 0;
        }
        else {
            modGroupBg = Colors.Transparent;
            bottomColor = dimLine;
            bottomBorderW = ModPanelUiMetrics.SidebarModListBottomBorderWidth;
        }
        box.BgColor = modGroupBg;
        if (selected) {
            box.BorderColor = ModPanelUiPalette.SidebarModActiveAccent;
            box.BorderWidthLeft = (int)ModPanelUiMetrics.SidebarModAccentBarWidth;
            box.BorderWidthTop = 0;
            box.BorderWidthRight = 0;
            box.BorderWidthBottom = 0;
        }
        else {
            box.BorderColor = bottomColor;
            box.BorderWidthLeft = 0;
            box.BorderWidthTop = 0;
            box.BorderWidthRight = 0;
            box.BorderWidthBottom = bottomBorderW;
        }
        box.CornerRadiusTopLeft = 0;
        box.CornerRadiusTopRight = 0;
        box.CornerRadiusBottomRight = 0;
        box.CornerRadiusBottomLeft = 0;
        box.ShadowSize = 0;
        box.ContentMarginLeft = 0;
        box.ContentMarginTop = 0;
        box.ContentMarginRight = 0;
        box.ContentMarginBottom = 0;
    }
    private static StyleBoxFlat CreateTransparentPanelStyle() {
        return new StyleBoxFlat {
            BgColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
            ShadowSize = 0,
            ContentMarginLeft = 0,
            ContentMarginTop = 0,
            ContentMarginRight = 0,
            ContentMarginBottom = 0,
        };
    }
    private static ColorRect CreateSidebarScrollTopDivider() {
        var a = ModPanelUiMetrics.SidebarModListSubtleAlpha;
        var alpha = Mathf.Clamp(a * 2.15f, 0.052f, 0.10f);
        return new ColorRect {
            CustomMinimumSize = new Vector2(0f, ModPanelUiMetrics.SidebarScrollTopDividerHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = new Color(1f, 1f, 1f, alpha),
        };
    }
    private static StyleBoxFlat CreateSidebarModVersionBadgeStyle() {
        var accent = ModPanelUiPalette.SidebarModActiveAccent;
        return new StyleBoxFlat {
            BgColor = new Color(0.058f, 0.052f, 0.045f, 0.96f),
            BorderColor = new Color(accent.R, accent.G, accent.B, 0.55f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusBottomLeft = 6,
            ContentMarginLeft = 10,
            ContentMarginTop = 4,
            ContentMarginRight = 10,
            ContentMarginBottom = 4,
            ShadowSize = 0,
        };
    }
    private static StyleBoxFlat CreateModSidebarPreviewFrameStyle() {
        return new StyleBoxFlat {
            BgColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
            CornerRadiusTopLeft = ModPanelUiMetrics.CornerRadius,
            CornerRadiusTopRight = ModPanelUiMetrics.CornerRadius,
            CornerRadiusBottomRight = ModPanelUiMetrics.CornerRadius,
            CornerRadiusBottomLeft = ModPanelUiMetrics.CornerRadius,
            ContentMarginLeft = 0,
            ContentMarginTop = 0,
            ContentMarginRight = 0,
            ContentMarginBottom = 0,
        };
    }
    private static MegaRichTextLabel CreateSidebarWrapLabel(int fontSize, HorizontalAlignment alignment,
        VerticalAlignment verticalAlignment = VerticalAlignment.Top) {
        var label = new MegaRichTextLabel {
            BbcodeEnabled = true,
            AutoSizeEnabled = false,
            ScrollActive = false,
            HorizontalAlignment = alignment,
            VerticalAlignment = verticalAlignment,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None,
            IsHorizontallyBound = true,
            FitContent = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("normal_font_size", fontSize);
        label.AddThemeFontSizeOverride("bold_font_size", fontSize);
        label.AddThemeFontSizeOverride("italics_font_size", fontSize);
        label.AddThemeFontSizeOverride("bold_italics_font_size", fontSize);
        label.AddThemeFontSizeOverride("mono_font_size", fontSize);
        ApplyMegaRichTextFontOverrides(label);
        label.MinFontSize = Math.Min(fontSize, 16);
        label.MaxFontSize = fontSize;
        return label;
    }
    private static MegaRichTextLabel CreateHeaderLabel(string text, int fontSize, HorizontalAlignment alignment,
        Color textModulate) {
        var label = new MegaRichTextLabel {
            BbcodeEnabled = true,
            AutoSizeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            ClipContents = false,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = alignment,
            IsHorizontallyBound = true,
            Modulate = textModulate,
        };
        label.AddThemeFontSizeOverride("normal_font_size", fontSize);
        label.AddThemeFontSizeOverride("bold_font_size", fontSize);
        label.AddThemeFontSizeOverride("italics_font_size", fontSize);
        label.AddThemeFontSizeOverride("bold_italics_font_size", fontSize);
        label.AddThemeFontSizeOverride("mono_font_size", fontSize);
        ApplyMegaRichTextFontOverrides(label);
        label.MinFontSize = Math.Max(14, fontSize - 3);
        label.MaxFontSize = fontSize;
        label.SetTextAutoSize(text);
        return label;
    }
    /// <summary>MegaRichTextLabel asserts font overrides at ready; shell is not under a themed menu root.</summary>
    private static void ApplyMegaRichTextFontOverrides(MegaRichTextLabel label) {
        var f = ThemeDB.FallbackFont;
        label.AddThemeFontOverride("normal_font", f);
        label.AddThemeFontOverride("bold_font", f);
        label.AddThemeFontOverride("italics_font", f);
        label.AddThemeFontOverride("bold_italics_font", f);
        label.AddThemeFontOverride("mono_font", f);
    }
    internal static void ApplyDevModeTabButtonStyle(Button b, bool selected) {
        var accent = ModPanelUiPalette.SidebarModActiveAccent;
        var selectedFill = new Color(accent.R, accent.G, accent.B, 0.20f);
        var flat = new StyleBoxFlat {
            BgColor = selected ? selectedFill : new Color(1f, 1f, 1f, 0.10f),
            BorderColor = selected ? new Color(accent.R, accent.G, accent.B, 0.88f) : KitLibTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 5,
            ContentMarginBottom = 5,
        };
        b.AddThemeStyleboxOverride("normal", flat);
        var hover = (StyleBoxFlat)flat.Duplicate();
        hover.BgColor = selected
            ? new Color(accent.R, accent.G, accent.B, 0.30f)
            : KitLibTheme.ButtonBgHover;
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", hover);
        b.AddThemeFontSizeOverride("font_size", 13);
        b.AddThemeColorOverride("font_color", selected ? KitLibTheme.TextPrimary : KitLibTheme.TextSecondary);
    }
    internal static Button CreateDevModePageTab(string pageId, string label, bool selected, Action onSelect) {
        var b = new Button {
            Text = label,
            ToggleMode = false,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(0f, 36f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        b.SetMeta("pageId", pageId);
        ApplyDevModeTabButtonStyle(b, selected);
        b.Pressed += onSelect;
        return b;
    }
    private const string ScopeStripTweenMeta = "dm_scope_tw";
    /// <summary>Bottom-left sidebar blurb: scope, wiring, and disclaimer (DevMode-owned shell, not Ritsu core).</summary>
    private static Control CreateModPanelScopeInfoStrip() {
        var accent = ModPanelUiPalette.SidebarModActiveAccent;
        var chrome = new StyleBoxFlat {
            BgColor = new Color(0.055f, 0.052f, 0.048f, 0.98f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            ShadowSize = 0,
        };
        var wrap = new PanelContainer {
            Name = "ModPanelScopeBlurb",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        wrap.AddThemeStyleboxOverride("panel", chrome);
        const string titleKey = "modpanel.sidebar.scope.title";
        const string titleFallback = "KitLib · Mod settings panel";
        const string bodyKey = "modpanel.sidebar.scope.body";
        const string bodyFallback =
            "When STS2-RitsuLib is enabled, this view layers Ritsu-registered settings onto the game's own mod list: the sidebar reflects manifest ids from the official scan, and the right pane shows pages whose ModId matches the selected entry, with known framework id aliases folded in. The stock mod UI may eventually absorb similar behavior; this stays a lightweight bridge outside RitsuLib core for stability. Use it mainly to troubleshoot and verify registration—it complements the official manager and is not a replacement for it.";
        const string expandKey = "modpanel.sidebar.scope.expand";
        const string expandFallback = "Show details";
        const string collapseKey = "modpanel.sidebar.scope.collapse";
        const string collapseFallback = "Hide details";
        const string collapsedSummaryKey = "modpanel.sidebar.scope.collapsedSummary";
        const string collapsedSummaryFallback = "Mod settings panel · click to expand";
        var root = new VBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        root.AddThemeConstantOverride("separation", 0);
        wrap.AddChild(root);
        var expandedStack = new VBoxContainer {
            Name = "ModPanelScopeExpanded",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        expandedStack.AddThemeConstantOverride("separation", 8);
        root.AddChild(expandedStack);
        var headerRow = new HBoxContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        headerRow.AddThemeConstantOverride("separation", 6);
        expandedStack.AddChild(headerRow);
        var title = new Label {
            Text = I18N.T(titleKey, titleFallback),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        title.AddThemeFontSizeOverride("font_size", 13);
        title.AddThemeColorOverride("font_color", accent);
        headerRow.AddChild(title);
        var collapseBtn = new Button {
            Name = "ModPanelScopeCollapse",
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(26, 26),
            Icon = MdiIcon.Minus.Texture(14, ModPanelUiPalette.RichTextMuted),
            TooltipText = I18N.T(collapseKey, collapseFallback),
        };
        ApplyScopeStripIconButton(collapseBtn);
        headerRow.AddChild(collapseBtn);
        var body = new Label {
            Text = I18N.T(bodyKey, bodyFallback),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeFontSizeOverride("font_size", 11);
        body.AddThemeColorOverride("font_color", ModPanelUiPalette.RichTextBody);
        expandedStack.AddChild(body);
        var collapsedBtn = new Button {
            Name = "ModPanelScopeCollapsed",
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            Flat = true,
            Text = I18N.T(collapsedSummaryKey, collapsedSummaryFallback),
            Icon = MdiIcon.ChevronRight.Texture(16, ModPanelUiPalette.RichTextMuted),
            IconAlignment = HorizontalAlignment.Left,
            Alignment = HorizontalAlignment.Left,
            TooltipText = I18N.T(expandKey, expandFallback),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 30),
        };
        collapsedBtn.AddThemeFontSizeOverride("font_size", 11);
        collapsedBtn.AddThemeColorOverride("font_color", ModPanelUiPalette.RichTextMuted);
        ApplyCollapsedScopeStripButton(collapsedBtn);
        root.AddChild(collapsedBtn);
        void ApplyChromeT(float t) {
            ApplyScopeStripChromeMargins(chrome, accent, t);
            wrap.QueueRedraw();
        }
        void SyncUiInstant(bool isExpanded) {
            ApplyChromeT(isExpanded ? 1f : 0f);
            expandedStack.Visible = isExpanded;
            collapsedBtn.Visible = !isExpanded;
            expandedStack.Modulate = new Color(1f, 1f, 1f, isExpanded ? 1f : 0f);
            collapsedBtn.Modulate = new Color(1f, 1f, 1f, isExpanded ? 0f : 1f);
        }
        void Persist(bool isExpanded) {
            SettingsStore.Current.ModPanelScopeStripExpanded = isExpanded;
            SettingsStore.Save();
        }
        void KillScopeTween() {
            if (!wrap.HasMeta(ScopeStripTweenMeta))
                return;
            var v = wrap.GetMeta(ScopeStripTweenMeta);
            if (v.VariantType == Variant.Type.Object && v.AsGodotObject() is Tween oldTw)
                oldTw.Kill();
            wrap.RemoveMeta(ScopeStripTweenMeta);
        }
        void RunToggle(bool toExpanded) {
            if (toExpanded == expandedStack.Visible)
                return;
            KillScopeTween();
            if (!GodotObject.IsInstanceValid(wrap) || !wrap.IsInsideTree()) {
                SyncUiInstant(toExpanded);
                Persist(toExpanded);
                return;
            }
            if (toExpanded) {
                var tw = wrap.CreateTween();
                wrap.SetMeta(ScopeStripTweenMeta, tw);
                tw.SetParallel(true);
                tw.TweenProperty(collapsedBtn, "modulate:a", 0f, 0.15f)
                    .SetEase(Tween.EaseType.In)
                    .SetTrans(Tween.TransitionType.Sine);
                tw.TweenMethod(
                        Callable.From((double u) => ApplyChromeT((float)u)),
                        0.0,
                        1.0,
                        0.22f)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Cubic);
                tw.SetParallel(false);
                tw.TweenCallback(Callable.From(() => {
                    collapsedBtn.Visible = false;
                    collapsedBtn.Modulate = Colors.White;
                    expandedStack.Visible = true;
                    expandedStack.Modulate = new Color(1f, 1f, 1f, 0f);
                }));
                tw.TweenProperty(expandedStack, "modulate:a", 1f, 0.2f)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Cubic);
                tw.TweenCallback(Callable.From(() => {
                    Persist(true);
                    if (wrap.HasMeta(ScopeStripTweenMeta))
                        wrap.RemoveMeta(ScopeStripTweenMeta);
                }));
            }
            else {
                var tw = wrap.CreateTween();
                wrap.SetMeta(ScopeStripTweenMeta, tw);
                tw.SetParallel(true);
                tw.TweenProperty(expandedStack, "modulate:a", 0f, 0.18f)
                    .SetEase(Tween.EaseType.In)
                    .SetTrans(Tween.TransitionType.Sine);
                tw.TweenMethod(
                        Callable.From((double u) => ApplyChromeT((float)u)),
                        1.0,
                        0.0,
                        0.2f)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Cubic);
                tw.SetParallel(false);
                tw.TweenCallback(Callable.From(() => {
                    expandedStack.Visible = false;
                    expandedStack.Modulate = Colors.White;
                    collapsedBtn.Visible = true;
                    collapsedBtn.Modulate = new Color(1f, 1f, 1f, 0f);
                }));
                tw.TweenProperty(collapsedBtn, "modulate:a", 1f, 0.16f)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Cubic);
                tw.TweenCallback(Callable.From(() => {
                    Persist(false);
                    if (wrap.HasMeta(ScopeStripTweenMeta))
                        wrap.RemoveMeta(ScopeStripTweenMeta);
                }));
            }
        }
        collapseBtn.Pressed += () => RunToggle(false);
        collapsedBtn.Pressed += () => RunToggle(true);
        SyncUiInstant(SettingsStore.Current.ModPanelScopeStripExpanded);
        wrap.TreeExiting += KillScopeTween;
        return wrap;
    }
    private static void ApplyScopeStripChromeMargins(StyleBoxFlat chrome, Color accent, float expandedT) {
        var u = Mathf.Clamp(expandedT, 0f, 1f);
        chrome.ContentMarginLeft = Mathf.RoundToInt(Mathf.Lerp(8f, 12f, u));
        chrome.ContentMarginRight = Mathf.RoundToInt(Mathf.Lerp(8f, 12f, u));
        chrome.ContentMarginTop = Mathf.RoundToInt(Mathf.Lerp(5f, 10f, u));
        chrome.ContentMarginBottom = Mathf.RoundToInt(Mathf.Lerp(5f, 10f, u));
        var cr = Mathf.RoundToInt(Mathf.Lerp(5f, 9f, u));
        chrome.CornerRadiusTopLeft = cr;
        chrome.CornerRadiusTopRight = cr;
        chrome.CornerRadiusBottomRight = cr;
        chrome.CornerRadiusBottomLeft = cr;
        var ba = Mathf.Lerp(0.38f, 0.65f, u);
        chrome.BorderColor = new Color(accent.R, accent.G, accent.B, ba);
        chrome.BgColor = new Color(Mathf.Lerp(0.045f, 0.055f, u), Mathf.Lerp(0.042f, 0.052f, u), Mathf.Lerp(0.038f, 0.048f, u), 0.98f);
    }
    private static void ApplyScopeStripIconButton(Button btn) {
        btn.Flat = true;
        var normal = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(1f, 1f, 1f, 0.06f);
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", normal);
    }
    private static void ApplyCollapsedScopeStripButton(Button btn) {
        var normal = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = new Color(1f, 1f, 1f, 0.05f);
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", normal);
    }
}
