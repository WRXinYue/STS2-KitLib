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
    private const float IconBtnSize = 36f;
    private const float OverlayW = 560f;
    private const int Radius = 14;

    // ── Panel geometry (shared by browser panels) ──
    // BrowserPanelLeft (= RailInsetW) is the single push amount: game content is shifted right
    // by this, so its new left edge, the rail's right edge, and the panel's left origin all align at 76.
    public const float BrowserRailLeft = 24f;
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

    // ── Frosted-glass blur backdrop ──
    private static ShaderMaterial? _railBackdropMat;

    /// <summary>
    /// Single-pass 2D gaussian blur reading the backbuffer (<c>screen_texture</c>) so the rail strip
    /// shows the game behind it blurred and slightly darkened, instead of a flat black veil.
    /// </summary>
    private static ShaderMaterial RailBackdropMaterial() {
        if (_railBackdropMat != null)
            return _railBackdropMat;
        _railBackdropMat = new ShaderMaterial {
            Shader = new Shader { Code = RailBackdropShaderCode }
        };
        return _railBackdropMat;
    }

    private const string RailBackdropShaderCode = """
        shader_type canvas_item;

        uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
        uniform float blur_radius = 6.0;
        uniform vec4 tint = vec4(0.0, 0.0, 0.0, 0.30);

        // Premultiplied-alpha correction, same as the official card blur shader.
        vec4 read_screen(vec2 uv) {
            vec4 c = textureLod(screen_texture, uv, 0.0);
            if (c.a > 0.0001)
                c.rgb /= c.a;
            return c;
        }

        void fragment() {
            vec2 px = SCREEN_PIXEL_SIZE;
            int taps = int(ceil(blur_radius));
            float sigma = max(1.0, blur_radius / 3.0);
            float s2 = 2.0 * sigma * sigma;

            vec4 acc = vec4(0.0);
            float wsum = 0.0;
            for (int x = -taps; x <= taps; x++) {
                for (int y = -taps; y <= taps; y++) {
                    float d = float(x * x + y * y);
                    float w = exp(-d / s2);
                    acc += read_screen(SCREEN_UV + vec2(float(x), float(y)) * px) * w;
                    wsum += w;
                }
            }

            vec3 blurred = (acc / wsum).rgb;
            vec3 col = mix(blurred, tint.rgb, tint.a);
            COLOR = vec4(col, 1.0);
        }
        """;

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

        // ── Backdrop for the left rail strip: frosted-glass gaussian blur of the game behind.
        // Background layers are left unshifted (see DevPanelUI.RailInset), so there is real content
        // behind this strip to sample. Hidden together with the rail via SlideRail.
        var railBackdrop = new ColorRect {
            Name = "RailBackdrop",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0,
            AnchorRight = 0,
            AnchorTop = 0,
            AnchorBottom = 1,
            OffsetLeft = 0,
            OffsetRight = BrowserPanelLeft,
            OffsetTop = 0,
            OffsetBottom = 0,
            Material = RailBackdropMaterial()
        };
        root.AddChild(railBackdrop);

        // Subtle 1px divider on the strip's right edge (child of backdrop so it hides together).
        var railDivider = new ColorRect {
            Name = "RailDivider",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0,
            AnchorRight = 0,
            AnchorTop = 0,
            AnchorBottom = 1,
            OffsetLeft = BrowserPanelLeft,
            OffsetRight = BrowserPanelLeft + 1,
            OffsetTop = 0,
            OffsetBottom = 0,
            Color = new Color(0.5f, 0.5f, 0.5f, 0.30f)
        };
        railBackdrop.AddChild(railDivider);

        // ── Icon Rail (left edge; transparent container so it sits in the reserved inset strip) ──
        var rail = new Control {
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

        // ── Persistent rail: always shown at the left edge (no slide-in/out, no auto-hide) ──
        float visibleX = 24f;
        rail.OffsetLeft = visibleX;
        rail.OffsetRight = visibleX + RailW;
        rail.Modulate = Colors.White;

        void SlideRail(bool show, bool userTriggered = false) {
            _railShown = show;
            railBackdrop.Visible = show;
            if (show) {
                rail.Visible = true;
                AttachRailInset(_railGlobalUi!);
                if (userTriggered && SettingsStore.ShouldShowRailIntroHint()) {
                    SettingsStore.MarkRailIntroDismissed();
                    RefreshPeekTabPresentation();
                }
            }
            else {
                rail.Visible = false;
                DetachRailInset();
            }
            SyncPeekTabCollapsedVisibility(true);
            RefreshRailHintPresentation();
        }

        BindRailSlide(SlideRail);

        // Poll only to keep log-alert hints fresh; rail itself never collapses.
        var pollTimer = new Godot.Timer {
            Name = "RailPollTimer",
            WaitTime = 0.1f,
            Autostart = true
        };

        pollTimer.Timeout += () => {
            RefreshLogAlertHints();
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
    }

    private static void OnRailLanguageChanged() {
        if (_railGlobalUi != null && GodotObject.IsInstanceValid(_railGlobalUi))
            RebuildRail(_railGlobalUi);
    }

    // ──────── Detach ────────
    public static void Detach(NGlobalUi globalUi) {
        KitLibModPanelOps.CancelHotkeySettingsCapture?.Invoke();
        DetachRailInset();
        _railGlobalUi = null;
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
