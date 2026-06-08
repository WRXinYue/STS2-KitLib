using System;
using System.Collections.Generic;
using KitLib;
using KitLib.Icons;
using KitLib.Multiplayer.Cheat;
using KitLib.Panels;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal const string RailRootName = "KitLibRailRoot";
    private const string LegacyTopBarName = "KitLibTopBar";
    private const string RootName = RailRootName;
    private const string OverlayName = "KitLibOverlay";
    private const float RailW = 52f;
    private const float IconBtnSize = 36f;
    private const float OverlayW = 560f;
    private const int Radius = 14;

    // ── Panel geometry (shared by browser panels) ──
    public const float BrowserRailLeft = 24f;
    public const float BrowserRailW = RailW;
    public const float BrowserPanelLeft = BrowserRailLeft + BrowserRailW;   // 76f
    public const float BrowserPanelRight = 24f;
    public const int BrowserRailRadius = Radius;

    private static Action? _onRefreshPanel;
    private static string? _activeOverlayId;
    private static readonly DevPanelController _controller = new();
    private static int _pinRailCount;
    private static int _browserOverlayCount;
    private static int _browserRailHoldCount;

    // ── Stored StyleBoxFlat refs for live theme refresh ──
    private static StyleBoxFlat? _railStyle;
    private static StyleBoxFlat? _railIndicatorStyle;
    private static StyleBoxFlat? _railSepStyle;
    private static int _activeRailBtnIdx = -1;
    private static readonly List<(Button btn, MdiIcon icon)> _railIconButtons = new();
    private static NGlobalUi? _railGlobalUi;

    /// <summary>Pin the rail visible (e.g. while an external overlay is open). Call Unpin when done.</summary>
    public static void PinRail() => _pinRailCount++;
    public static void UnpinRail() => _pinRailCount = Math.Max(0, _pinRailCount - 1);

    /// <summary>
    /// Remove (joined=true) or restore (joined=false) the rail's right border/radius so browser
    /// panels appear seamlessly connected.
    /// </summary>
    public static void SpliceRail(NGlobalUi globalUi, bool joined) {
        var railRoot = ((Node)globalUi).GetNodeOrNull<Control>(RootName);
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
        if (!browserOpen)
            UnwireBrowserPanelMergeTracking();
        ScheduleBrowserContextMerge(globalUi);
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
        if (_railStyle != null) {
            _railStyle.BgColor = ColRailBg;
            _railStyle.BorderColor = ColRailBorder;
        }
        if (_railIndicatorStyle != null)
            _railIndicatorStyle.BgColor = ColIconActiveBg;
        if (_railSepStyle != null)
            _railSepStyle.BgColor = ColSeparator;
        ApplyPeekTabTheme();

        RefreshRailIconTints();
        ApplyContextPaneTheme();
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
            btn.Icon = icon.Texture(20, disabled ? ColIconDisabled : active ? ColIconActive : ColIconNormal);
        }
    }

    // ──────── Attach ────────
    public static void Attach(NGlobalUi globalUi, DevPanelActions actions) {
        if (((Node)globalUi).GetNodeOrNull<Control>(RootName) != null)
            return;

        _railGlobalUi = globalUi;
        _onRefreshPanel = actions.OnRefreshPanel;
        _activeOverlayId = null;
        _controller.Attach(closeAllPanels: () => {
            HoldBrowserRail(globalUi);
            CloseOverlay(globalUi);
            var parent = (Node)globalUi;
            foreach (var child in parent.GetChildren()) {
                if (child is Control ctrl) {
                    string name = ctrl.Name.ToString();
                    if (name.StartsWith("KitLib", StringComparison.Ordinal)
                        && !_keepNodes.Contains(name)) {
                        if (!TryAnimateBrowserOverlayClose(parent, ctrl)) {
                            parent.RemoveChild(ctrl);
                            ctrl.QueueFree();
                        }
                    }
                }
            }
            Callable.From(() => ReleaseBrowserRail(globalUi)).CallDeferred();
        });
        _browserOverlayCount = 0;
        _browserRailHoldCount = 0;
        _railStyle = null;
        _railIndicatorStyle = null;
        _railSepStyle = null;
        _activeRailBtnIdx = -1;
        _railIconButtons.Clear();

        ThemeManager.OnThemeChanged -= ApplyRailTheme;
        ThemeManager.OnThemeChanged += ApplyRailTheme;

        var root = new Control {
            Name = RootName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 1200
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // ── Icon Rail (left edge, full height, rounded right corners) ──
        var rail = new PanelContainer {
            Name = "Rail",
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 0,
            AnchorRight = 0,
            AnchorTop = 0.15f,
            AnchorBottom = 0.85f,
            OffsetLeft = 24,
            OffsetRight = 24 + RailW,
            OffsetTop = 0,
            OffsetBottom = 0
        };
        _railStyle = new StyleBoxFlat {
            BgColor = ColRailBg,
            CornerRadiusTopLeft = Radius,
            CornerRadiusBottomLeft = Radius,
            CornerRadiusTopRight = Radius,
            CornerRadiusBottomRight = Radius,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = ColRailBorder,
            ShadowColor = new Color(0, 0, 0, 0.25f),
            ShadowSize = 8
        };
        rail.AddThemeStyleboxOverride("panel", _railStyle);

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

        // ── Spacer ──
        railVBox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

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

        // ── Auto-hide: timer-based mouse position polling ──
        float hiddenX = -(24 + RailW);
        float visibleX = 24f;
        Tween? railTween = null;

        rail.OffsetLeft = hiddenX;
        rail.OffsetRight = hiddenX + RailW;
        rail.Modulate = new Color(1, 1, 1, 0);

        void SlideRail(bool show, bool userTriggered = false) {
            if (_railShown == show) return;

            if (show && userTriggered && SettingsStore.ShouldShowRailIntroHint()) {
                SettingsStore.MarkRailIntroDismissed();
                RefreshPeekTabPresentation();
            }

            _railShown = show;

            railTween?.Kill();
            railTween = rail.CreateTween();

            float targetLeft = show ? visibleX : hiddenX;
            float targetRight = targetLeft + RailW;
            float targetAlpha = show ? 1f : 0f;

            railTween.TweenProperty(rail, "offset_left", targetLeft, 0.2f)
                     .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            railTween.Parallel()
                     .TweenProperty(rail, "offset_right", targetRight, 0.2f)
                     .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            railTween.Parallel()
                     .TweenProperty(rail, "modulate:a", targetAlpha, 0.15f)
                     .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

            SetPeekTabVisible(!show);
            RefreshRailHintPresentation();
        }

        BindRailSlide(SlideRail);

        var pollTimer = new Godot.Timer {
            Name = "RailPollTimer",
            WaitTime = 0.1f,
            Autostart = true
        };
        float hitZoneRight = visibleX + RailW + 16f;

        pollTimer.Timeout += () => {
            if (_activeOverlayId != null || _pinRailCount > 0 || _keyboardRailPinned) {
                if (!_railShown) SlideRail(true);
                SetPeekTabVisible(false);
                StopPeekTabPresentation();
                RefreshLogAlertHints();
                return;
            }

            var mousePos = root.GetViewport().GetMousePosition();
            var railRect = rail.GetGlobalRect();
            bool inHitZone = mousePos.X < hitZoneRight
                          && mousePos.Y > railRect.Position.Y - 20
                          && mousePos.Y < railRect.End.Y + 20;
            bool overRail = _railShown && railRect.Grow(8).HasPoint(mousePos);

            if (inHitZone || overRail)
                SlideRail(true, userTriggered: true);
            else if (_railShown)
                SlideRail(false);

            RefreshRailHintPresentation();
        };
        root.AddChild(pollTimer);

        WirePeekTabPressed(() => SlideRail(true, userTriggered: true));
        RefreshPeekTabHotkeyHint();

        RefreshRailHintPresentation();

        if (!KitLibState.DualInstanceMinimalRail)
            AttachContextPane(globalUi);
        ((Node)globalUi).AddChild(root);
    }

    // ──────── Detach ────────
    public static void Detach(NGlobalUi globalUi) {
        DevPanelHotkeySettingsUI.CancelCapture();
        _railGlobalUi = null;
        _activeOverlayId = null;
        ResetRailHotkeyState();
        _controller.Detach();
        _browserOverlayCount = 0;
        _browserRailHoldCount = 0;
        _pinRailCount = 0;
        ThemeManager.OnThemeChanged -= ApplyRailTheme;
        TeardownPeekTab();
        StopLogAlertBlink();
        _railStyle = null;
        _railIndicatorStyle = null;
        _railSepStyle = null;
        _railIconButtons.Clear();
        _railButtons.Clear();
        _railVBox = null;
        _railIndicator = null;
        _moveRailIndicator = null;
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
        DetachContextPane(globalUi);
        ((Node)globalUi).GetNodeOrNull<Control>(LegacyTopBarName)?.QueueFree();
        _onRefreshPanel = null;
    }

    // ──────── Close all known overlays (internal + external UIs) ────────
    private static readonly HashSet<string> _keepNodes = new() { RootName, ContextPaneRootName };

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

        var root = ((Node)globalUi).GetNodeOrNull<Control>(RootName);
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
        var root = ((Node)globalUi).GetNodeOrNull<Control>(RootName);
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
