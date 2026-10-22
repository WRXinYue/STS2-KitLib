using System;
using System.Collections.Generic;
using Godot;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Icons;
using KitLib.Multiplayer.Cheat;
using KitLib.Panels;
using KitLib.Settings;
using KitLib.UI.Diagnostics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal const string RailRootName = "KitLibRailRoot";
    private const string LegacyTopBarName = "KitLibTopBar";
    private const string RootName = RailRootName;
    private const string OverlayName = "KitLibOverlay";

    internal static Control? TryGetRailRoot(NGlobalUi? globalUi = null) =>
        globalUi != null ? ((Node)globalUi).GetNodeOrNull<Control>(RootName) : null;
    private const float RailW = 52f;
    private const float IconBtnSize = KitLibRailChrome.IconButtonSize;
    private const float OverlayW = 560f;
    private const int Radius = 14;

    // ── Panel geometry (shared by browser panels) ──
    // BrowserPanelLeft (= RailInsetW) is the single push amount: game content is shifted right
    // by this, so its new left edge, the rail's right edge, and the panel's left origin all align at 76.
    public const float BrowserRailLeft = KitLibRailChrome.RailLeft;
    public const float BrowserRailW = RailW;
    public const float BrowserPanelLeft = BrowserRailLeft + BrowserRailW;   // 76f
    public const float BrowserPanelRight = 24f;
    public const int BrowserOverlayZIndex = 1250;
    /// <summary>Left rail chrome — above browser slide-ins.</summary>
    public const int SidebarChromeZIndex = BrowserOverlayZIndex + 10;
    public const int BrowserRailRadius = Radius;

    private static Action? _onRefreshPanel;
    private static string? _activeOverlayId;
    private static readonly DevPanelController _controller = new();
    private static int _pinRailCount;
    private static int _browserOverlayCount;
    private static int _browserRailHoldCount;

    // ── Stored StyleBoxFlat refs for live theme refresh ──
    private static StyleBoxFlat? _railIndicatorStyle;
    private static StyleBoxFlat? _railSepStyle;
    private static int _activeRailBtnIdx = -1;
    private static readonly List<(Button btn, MdiIcon icon)> _railIconButtons = new();
    private static NGlobalUi? _railGlobalUi;

    private static float _railTaperEndY = -1f;

    /// <summary>Screen Y below which the widened strip has bent back onto the frame edge; &lt;= 0 means full height.</summary>
    internal static float RailTaperEndY => _railTaperEndY;

    /// <summary>Pin the rail visible (e.g. while an external overlay is open). Call Unpin when done.</summary>
    public static void PinRail() => _pinRailCount++;
    public static void UnpinRail() => _pinRailCount = Math.Max(0, _pinRailCount - 1);

    /// <summary>
    /// Remove (joined=true) or restore (joined=false) the rail's right border/radius so browser
    /// panels appear seamlessly connected.
    /// </summary>
    public static void SpliceRail(NGlobalUi globalUi, bool joined) {
        var railRoot = TryGetRailRoot(globalUi);
        var rail = railRoot?.GetNodeOrNull<PanelContainer>("Rail");
        if (rail == null) return;

        if (rail.GetThemeStylebox("panel") is StyleBoxFlat sb) {
            int r = joined ? 0 : BrowserRailRadius;
            sb.CornerRadiusTopRight = r;
            sb.CornerRadiusBottomRight = r;
            sb.BorderWidthRight = joined ? 0 : 1;
        }
    }

    private static void HoldBrowserRail(NGlobalUi globalUi) {
        _browserRailHoldCount++;
        ReconcileBrowserRail(globalUi);
    }

    private static void ReleaseBrowserRail(NGlobalUi globalUi) {
        _browserRailHoldCount = Math.Max(0, _browserRailHoldCount - 1);
        ReconcileBrowserRail(globalUi);
    }

    private static void ReconcileBrowserRail(NGlobalUi globalUi) {
        bool browserOpen = (_browserOverlayCount + _browserRailHoldCount) > 0;
        SpliceRail(globalUi, browserOpen);
    }

    // ── Colour palette — delegates to active theme ──
    private static Color ColRailBg => ThemeManager.Current.RailBg;
    private static Color ColRailBorder => ThemeManager.Current.RailBorder;
    private static Color ColIconNormal => ThemeManager.Current.IconNormal;
    private static Color ColIconHover => ThemeManager.Current.IconHover;
    private static Color ColIconActive => KitLibTheme.Accent;
    private static Color ColIconDisabled => new(0.55f, 0.55f, 0.55f, 0.85f);
    private static Color ColIconActiveBg => ThemeManager.Current.IconActiveBg;
    private static Color ColOverlayBg => KitLibTheme.PanelBg;
    private static Color ColOverlayBorder => KitLibTheme.PanelBorder;
    private static readonly Color ColBackdrop = new(0f, 0f, 0f, 0.50f);
    private static Color ColSectionText => KitLibTheme.Subtle;
    private static Color ColSeparator => KitLibTheme.Separator;

    /// <summary>Applies the current theme colors to the persistent rail widgets in-place.</summary>
    private static void ApplyRailTheme() {
        if (_railIndicatorStyle != null)
            _railIndicatorStyle.BgColor = ColIconActiveBg;
        if (_railSepStyle != null)
            _railSepStyle.BgColor = ColSeparator;
        ApplyPeekTabTheme();

        RefreshRailIconTints();
        RefreshRailHintPresentation();
    }

    internal static void RefreshRailHintPresentation() {
        RefreshPeekTabPresentation();
        RefreshLogAlertHints();
        RefreshRailTabAvailability();
    }

    private static void RefreshRailIconTints() {
        for (int i = 0; i < _railIconButtons.Count; i++) {
            var (btn, icon) = _railIconButtons[i];
            if (IsLogAlertBlinking(btn))
                continue;
            bool disabled = btn.Disabled;
            bool active = !disabled && i == _activeRailBtnIdx;
            var tint = disabled ? ColIconDisabled : active ? ColIconActive : ColIconNormal;
            btn.Icon = ResolveRailIconTexture(icon, tint);
        }
    }

    static ImageTexture? ResolveRailIconTexture(MdiIcon icon, Color tint) {
        var tex = icon.Texture(20, tint);
        if (tex != null)
            return tex;
        return MdiIcon.PuzzleOutline.Texture(20, tint);
    }

    // ── Frosted-glass blur backdrop — shared with the main-menu rail via KitLibRailChrome ──

    // ──────── Attach ────────
    public static void Attach(NGlobalUi globalUi, DevPanelActions actions) {
        if (TryGetRailRoot(globalUi) != null)
            return;

        _railGlobalUi = globalUi;
        _onRefreshPanel = actions.OnRefreshPanel;
        _activeOverlayId = null;
        _controller.Attach(
            hideAllPanels: () => HideAllSessionOverlays(globalUi),
            destroyAllPanels: () => DestroyAllSessionOverlays(globalUi));
        _browserOverlayCount = 0;
        _browserRailHoldCount = 0;
        _railIndicatorStyle = null;
        _railSepStyle = null;
        _activeRailBtnIdx = -1;
        _railIconButtons.Clear();

        ThemeManager.OnThemeChanged -= ApplyRailTheme;
        ThemeManager.OnThemeChanged += ApplyRailTheme;
        I18N.LanguageChanged -= OnRailLanguageChanged;
        I18N.LanguageChanged += OnRailLanguageChanged;

        var root = new Control {
            Name = RootName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = SidebarChromeZIndex
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // ── Left contour band: the screen frame's left border widened to this rail's strip, so the
        // rail lives inside one continuous window edge instead of beside it. The frame layer cuts its
        // own inner stroke here (SetLeftBand) and the band draws that boundary instead. Game content
        // it would cover is pushed right by BrowserPanelLeft (see RailInset).
        // Hidden together with the rail via SlideRail.
        var railBackdrop = KitLibScreenFrame.CreateLeftBand(BrowserPanelLeft);
        root.AddChild(railBackdrop);

        // ── Icon Rail (left edge; transparent container so it sits in the reserved inset strip) ──
        var rail = new Control {
            Name = "Rail",
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 0,
            AnchorRight = 0,
            AnchorTop = 0.15f,
            AnchorBottom = 0.85f,
            OffsetLeft = BrowserRailLeft,
            OffsetRight = BrowserPanelLeft,
            OffsetTop = 0,
            OffsetBottom = 0
        };

        // Wrapper allows absolute positioning for the sliding indicator
        var railWrapper = new Control {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        // Sliding indicator (drawn behind buttons, rounded corners)
        var railIndicator = new Panel {
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = 2,
            OffsetRight = -2,
            OffsetTop = 0,
            OffsetBottom = IconBtnSize,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _railIndicatorStyle = new StyleBoxFlat {
            BgColor = ColIconActiveBg,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        railIndicator.AddThemeStyleboxOverride("panel", _railIndicatorStyle);
        railWrapper.AddChild(railIndicator);

        var railVBox = new VBoxContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        railVBox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        railVBox.AddThemeConstantOverride("separation", 2);

        _railVBox = railVBox;
        _railIndicator = railIndicator;

        PopulatePrimaryRailButtons(globalUi, railVBox, _railButtons);

        // ── Separator line ──
        if (!KitLibState.DualInstanceMinimalRail) {
            var sep = new HSeparator();
            _railSepStyle = new StyleBoxFlat {
                BgColor = ColSeparator,
                ContentMarginTop = 0,
                ContentMarginBottom = 0,
                ContentMarginLeft = 4,
                ContentMarginRight = 4
            };
            sep.AddThemeStyleboxOverride("separator", _railSepStyle);
            sep.AddThemeConstantOverride("separation", 8);
            railVBox.AddChild(sep);

            PopulateUtilityRailButtons(globalUi, railVBox, _railButtons);
        }
        WireRailIndicator(railIndicator, _railButtons);

        railWrapper.AddChild(railVBox);
        rail.AddChild(railWrapper);

        root.AddChild(rail);

        CreatePeekTab(root);

        // ── Rail docked at the left edge; it retracts with the screen frame's wrap (Ctrl+B) instead of
        // sliding on its own, so the whole window edge leaves and returns as one piece.
        float visibleX = BrowserRailLeft;
        rail.OffsetLeft = visibleX;
        rail.OffsetRight = visibleX + RailW;
        rail.Modulate = Colors.White;

        void SlideRail(bool show, bool userTriggered = false) {
            _railShown = show;
            KitLibScreenFrame.SetWrapShown(show, animated: true);
            if (show && userTriggered && SettingsStore.ShouldShowRailIntroHint()) {
                SettingsStore.MarkRailIntroDismissed();
                RefreshPeekTabPresentation();
            }
            SyncPeekTabCollapsedVisibility(true);
            RefreshRailHintPresentation();
        }

        BindRailSlide(SlideRail);

        // Poll only to keep log-alert hints fresh; rail itself never collapses on its own.
        var pollTimer = new Godot.Timer {
            Name = "RailPollTimer",
            WaitTime = 0.1f,
            Autostart = true
        };

        pollTimer.Timeout += () => {
            RefreshLogAlertHints();
            // Re-assert the band so the frame's stroke cut keeps tracking this rail during scene swaps.
            ApplyWrapState();
            if (_activeOverlayId != null || _pinRailCount > 0) {
                SetPeekTabVisible(false);
                StopPeekTabPresentation();
            }
        };
        root.AddChild(pollTimer);

        // Peek tab stays wired but never shows (rail is persistent).
        WirePeekTabPressed(() => SlideRail(true, userTriggered: true));
        SyncPeekTabCollapsedVisibility(true);
        RefreshPeekTabHotkeyHint();

        RefreshRailHintPresentation();

        if (!KitLibState.DualInstanceMinimalRail) {
            MonsterIntentOverlayUI.Attach(globalUi);
            MonsterIntentOverlayUI.SyncState(globalUi);
        }
        ((Node)globalUi).AddChild(root);
        root.TopLevel = true;
        AttachRailInset(globalUi);
        _railShown = true;
        // Rail starts shown, so it is placed (and its contour reported) at the current wrap right away.
        KitLibScreenFrame.AddWrapListener(ApplyWrapState);
        ApplyWrapState();
    }

    /// <summary>
    /// Place the rail and its widened contour from the current wrap progress. Registered as a wrap
    /// listener so a Ctrl+B retract carries the rail out and back in with the screen frame, and called
    /// by the poll timer to keep the frame's stroke cut tied to this rail across scene swaps.
    /// </summary>
    private static void ApplyWrapState() {
        if (_railGlobalUi == null || !GodotObject.IsInstanceValid(_railGlobalUi)) {
            _railTaperEndY = -1f;
            return;
        }

        float t = KitLibScreenFrame.WrapT;
        // Ctrl+B drives the wrap directly, so the local shown flag follows it to keep rail hotkeys,
        // tab activation and the dismiss logic working against the real state.
        _railShown = KitLibScreenFrame.WrapShown;
        var root = TryGetRailRoot(_railGlobalUi);
        var rail = root?.GetNodeOrNull<Control>("Rail");
        var band = root?.GetNodeOrNull<ColorRect>(KitLibScreenFrame.LeftBandNodeName);

        if (rail != null) {
            // Slide fully off screen as the wrap retracts; the rail's right edge lands on x=0 at t=0.
            float left = BrowserRailLeft - (1f - t) * BrowserPanelLeft;
            rail.OffsetLeft = left;
            rail.OffsetRight = left + RailW;
            // Stay visible through the retract so the slide is seen, then hide once fully off screen.
            rail.Visible = _railShown || t > 0.001f;
        }

        if (band != null)
            band.Visible = t > 0.001f;

        var (stepY, stepCurve) = RailBandTaper();
        _railTaperEndY = stepY;
        KitLibScreenFrame.SetLeftBand(band, BrowserPanelLeft * t, stepY, stepCurve);
    }

    // Same taper shape as the main-menu rail, so both strips bend back identically.
    private const float BandStepClearance = 6f;
    private const float BandStepCurveRatio = 0.5f;
    private const float BandStepCurveMin = 140f;
    private const float BandStepCurveMax = 200f;

    /// <summary>
    /// <para xml:lang="en">Where this rail's widened strip bends back onto the frame's own edge: just past the last
    /// visible entry plus the taper arc, so the spare room below never reads as a wide empty band.</para>
    /// <para xml:lang="zh-CN">本栏加宽带收窄的位置：最后一个可见条目之后加清空余量与弧长，下方空余不再是一条宽黑带。</para>
    /// </summary>
    private static (float StepY, float Curve) RailBandTaper() {
        float top = float.PositiveInfinity;
        float bottom = float.NegativeInfinity;
        foreach (var (btn, _) in _railIconButtons) {
            if (!GodotObject.IsInstanceValid(btn) || !btn.Visible)
                continue;

            var rect = btn.GetGlobalRect();
            top = Mathf.Min(top, rect.Position.Y);
            bottom = Mathf.Max(bottom, rect.End.Y);
        }

        // Buttons have no rect until the first layout pass; keep the strip full height until then.
        if (float.IsNegativeInfinity(bottom) || bottom <= 0f)
            return (-1f, 0f);

        float curve = Mathf.Clamp((bottom - top) * BandStepCurveRatio, BandStepCurveMin, BandStepCurveMax);
        return (bottom + BandStepClearance + curve, curve);
    }

    private static void OnRailLanguageChanged() {
        if (_railGlobalUi != null && GodotObject.IsInstanceValid(_railGlobalUi))
            RebuildRail(_railGlobalUi);
    }

    // ──────── Detach ────────
    public static void Detach(NGlobalUi globalUi) {
        KitLibModPanelOps.CancelHotkeySettingsCapture?.Invoke();
        KitLibScreenFrame.RemoveWrapListener(ApplyWrapState);
        DetachRailInset();
        KitLibScreenFrame.SetLeftBand(0f);
        // The frame is a permanent decoration: restore it so the next scene starts with a full edge.
        KitLibScreenFrame.SetWrapShown(true, animated: false);
        _railGlobalUi = null;
        _railTaperEndY = -1f;
        _activeOverlayId = null;
        ResetRailHotkeyState();
        DestroyAllSessionOverlays(globalUi);
        _controller.Detach();
        _browserOverlayCount = 0;
        _browserRailHoldCount = 0;
        _pinRailCount = 0;
        ThemeManager.OnThemeChanged -= ApplyRailTheme;
        I18N.LanguageChanged -= OnRailLanguageChanged;
        TeardownPeekTab();
        StopLogAlertBlink();
        _railIndicatorStyle = null;
        _railSepStyle = null;
        _railIconButtons.Clear();
        _railButtons.Clear();
        _railVBox = null;
        if (_railIndicatorTween != null && GodotObject.IsInstanceValid(_railIndicatorTween))
            _railIndicatorTween.Kill();
        _railIndicatorTween = null;
        _railIndicator = null;
        _moveRailIndicator = null;
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
        MonsterIntentOverlayUI.Detach(globalUi);
        ((Node)globalUi).GetNodeOrNull<Control>(LegacyTopBarName)?.QueueFree();
        _onRefreshPanel = null;
    }

    // ──────── Close all known overlays (internal + external UIs) ────────
    private static readonly HashSet<string> _keepNodes = new() { RootName };

    /// <summary>
    /// Close the internal overlay (cheats/save/ai) and remove all DevMode external
    /// panels from globalUi. Delegates to <see cref="DevPanelController.CloseAll"/>
    /// so all panel-lifecycle decisions stay in the controller layer.
    /// </summary>
    public static void CloseAllOverlays(NGlobalUi globalUi) => _controller.CloseVisuals();

    // ──────── Overlay: toggle / close ────────
    private static void ToggleOverlay(NGlobalUi globalUi, string id, Action<Control> buildContent) {
        if (_activeOverlayId == id) {
            CloseOverlay(globalUi);
            return;
        }

        CloseOverlay(globalUi);
        _activeOverlayId = id;

        var root = TryGetRailRoot(globalUi);
        if (root == null) return;

        var clickaway = new Control {
            Name = "OverlayClickaway",
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 1,
            OffsetLeft = RailW + 32,
            OffsetRight = 0,
            OffsetTop = 0,
            OffsetBottom = 0
        };
        clickaway.GuiInput += e => {
            if (e is InputEventMouseButton { Pressed: true })
                CloseOverlay(globalUi);
        };
        root.AddChild(clickaway);
        root.MoveChild(clickaway, 0);

        var panel = CreateMainMenuModalPanel(OverlayW);
        panel.Name = OverlayName;
        root.AddChild(panel);

        var content = panel.GetNode<VBoxContainer>("Content");
        buildContent(content);
    }

    private static void CloseOverlay(NGlobalUi globalUi) {
        var root = TryGetRailRoot(globalUi);
        if (root == null) { _activeOverlayId = null; return; }

        var clickaway = root.GetNodeOrNull<Control>("OverlayClickaway");
        if (clickaway != null) {
            root.RemoveChild(clickaway);
            clickaway.QueueFree();
        }

        var panel = root.GetNodeOrNull<PanelContainer>(OverlayName);
        if (panel != null) {
            root.RemoveChild(panel);
            panel.QueueFree();
        }
        _activeOverlayId = null;
    }
}
