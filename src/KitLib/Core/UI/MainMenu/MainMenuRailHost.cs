using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using KitLib.Abstractions.Modding;
using KitLib.Host;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

/// <summary>
/// Left-edge icon rail for the main menu. Hosts the tools that content mods register through
/// <see cref="KitLibMainMenuCornerButtonRegistry"/> and replaces the top-right corner icon stack
/// (<see cref="MainMenuCornerButtonHost"/>) while attached, so each tool keeps a single entry point.
/// </summary>
/// <remarks>
/// Lives in Core rather than in the DevTools satellite so the rail stays available even when the
/// satellite modules are disabled, and so it never depends on run-only nodes (<c>NGlobalUi</c>).
/// </remarks>
internal static class MainMenuRailHost {
    internal const string RootNodeName = "KitLibMainMenuRailRoot";
    private const float ButtonGap = 2f;
    private const int IndicatorRadius = 8;
    private const float RefreshIntervalSeconds = 0.1f;
    private const string OpenMetaKey = "kitlib_rail_open";
    private const float MenuRailButtonWidth = 80f;
    private const float ContentPadding = 10f;
    private const float LabelFontSize = 11f;
    private const string LabelFontPath = "res://themes/kreon_regular_shared.tres";
    private static readonly Color HoverBg = new(1f, 1f, 1f, 0.10f);
    private static readonly Color PressedBg = new(1f, 1f, 1f, 0.16f);
    private static readonly Color LabelColor = new(0.92f, 0.92f, 0.92f, 0.95f);
    private static Font? _labelFont;
    private static bool _labelFontProbed;

    /// <summary>Width reserved for the menu rail; other left-docked panels start after it.</summary>
    internal const float MenuRailInset = KitLibRailChrome.RailLeft + MenuRailButtonWidth;

    /// <summary>Left offset of the button column; the strip's padding is halved so the column sits centred.</summary>
    private const float RailColumnLeft = (MenuRailInset - MenuRailButtonWidth) * 0.5f;

    private static NMainMenu? _menu;
    private static Control? _root;
    private static Control? _rail;
    private static ColorRect? _band;
    private static Panel? _indicator;
    private static VBoxContainer? _buttons;
    private static readonly List<Button> _railButtons = new();
    private static string _signature = "";

    /// <summary>Whether the menu sits inside the official multiplayer flow; solo-only entries hide while it does.</summary>
    private static bool _inMultiplayerFlow;

    // ── Rail presence ──
    // 0 = retracted off screen, 1 = docked. Own tween for the panel-driven appearance, capped by the
    // screen frame's wrap so Ctrl+B retracting the window edge takes the rail with it.
    private static float _presence;
    private static float _presenceTarget;
    private static bool _wantPresence;
    private static Tween? _presenceTween;

    /// <summary>Vanilla chrome pushed right to clear the strip, kept with its original offsets.</summary>
    private static readonly List<PushedChrome> _pushed = new();

    /// <summary>Y below which the widened strip has arced back to the frame's own edge; &lt;= 0 keeps
    /// it full height.</summary>
    private static float _bandStepY = -1f;

    /// <summary>Length the strip's widened edge bends back over, in logical pixels.</summary>
    private static float _bandStepCurve;

    /// <summary>Share of the rail's content height the taper spans, clamped to these bounds in logical pixels.</summary>
    private const float BandStepCurveRatio = 0.5f;
    private const float BandStepCurveMin = 140f;
    private const float BandStepCurveMax = 200f;

    /// <summary>Screen-space Y to pin the strip's taper to; &lt;= 0 (default) follows the rail's content instead.</summary>
    internal static float BandStepOverride { get; set; } = -1f;

    /// <summary>Whether the rail is currently hosted by <paramref name="mainMenu"/>.</summary>
    internal static bool IsAttachedTo(NMainMenu mainMenu) =>
        _root != null && _menu == mainMenu &&
        GodotObject.IsInstanceValid(_root) && _root.IsInsideTree();

    /// <summary>Attach the rail to the main menu, or refresh it when already attached.</summary>
    public static void EnsureAttached(NMainMenu mainMenu) {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return;

        if (IsAttachedTo(mainMenu)) {
            Sync(mainMenu);
            return;
        }

        Detach();
        _menu = mainMenu;
        _root = CreateRoot(mainMenu);
        Sync(mainMenu);
    }

    /// <summary>Sync the rail on the currently loaded main menu, if any.</summary>
    public static void SyncActiveMainMenu() {
        try {
            var mainMenu = MegaCrit.Sts2.Core.Nodes.NGame.Instance?.MainMenu;
            if (mainMenu != null && GodotObject.IsInstanceValid(mainMenu))
                Sync(mainMenu);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib main-menu rail sync failed: {ex.Message}");
        }
    }

    private static Control CreateRoot(NMainMenu mainMenu) {
        var root = new Control {
            Name = RootNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = KitLibRailChrome.ChromeZIndex,
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // Left contour band: the screen frame's left border widened to this rail's strip, so the rail
        // lives inside one continuous window edge instead of beside it. The frame layer cuts its own
        // inner stroke here (SetLeftBand) and the band draws that boundary instead. Menu buttons it
        // would cover are pushed right by MenuRailInset (see ApplyInsets).
        var backdrop = KitLibScreenFrame.CreateLeftBand(MenuRailInset);
        root.AddChild(backdrop);
        _band = backdrop;

        var rail = new Control {
            Name = "Rail",
            // Decoration only: the buttons inside take the clicks, so chrome under the strip stays clickable.
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0,
            AnchorRight = 0,
            AnchorTop = 0.07f,
            AnchorBottom = 0.85f,
            OffsetLeft = RailColumnLeft,
            OffsetRight = RailColumnLeft + MenuRailButtonWidth,
            OffsetTop = 0,
            OffsetBottom = 0,
        };
        root.AddChild(rail);
        _rail = rail;

        // Wrapper holds the sliding indicator and the button column in one coordinate space,
        // so the indicator can be positioned from a button's Y.
        var wrapper = new Control {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        wrapper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        rail.AddChild(wrapper);

        _indicator = new Panel {
            Name = "RailIndicator",
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = 2,
            OffsetRight = -2,
            OffsetTop = 0,
            OffsetBottom = KitLibRailChrome.IconButtonSize,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _indicator.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = ThemeManager.Current.IconActiveBg,
            CornerRadiusTopLeft = IndicatorRadius,
            CornerRadiusTopRight = IndicatorRadius,
            CornerRadiusBottomLeft = IndicatorRadius,
            CornerRadiusBottomRight = IndicatorRadius,
        });
        wrapper.AddChild(_indicator);

        _buttons = new VBoxContainer {
            Name = "RailButtons",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _buttons.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _buttons.AddThemeConstantOverride("separation", (int)ButtonGap);
        wrapper.AddChild(_buttons);

        var timer = new Godot.Timer {
            Name = "RailRefresh",
            WaitTime = RefreshIntervalSeconds,
            Autostart = true,
        };
        timer.Timeout += () => {
            var menu = _menu;
            if (menu == null || !GodotObject.IsInstanceValid(menu) || !GodotObject.IsInstanceValid(root)) {
                ResetStatics();
                return;
            }
            Sync(menu);
        };
        root.AddChild(timer);

        mainMenu.AddChild(root);
        mainMenu.MoveChild(root, Math.Max(0, mainMenu.GetChildCount() - 1));
        MainFile.Logger.Info("KitLib main-menu rail attached.");
        // Registered so a Ctrl+B wrap retract carries this rail out and back in with the window edge.
        KitLibScreenFrame.AddWrapListener(ApplyMenuWrap);
        return root;
    }

    private static void Sync(NMainMenu mainMenu) {
        if (_root == null || !GodotObject.IsInstanceValid(_root)) {
            ResetStatics();
            return;
        }

        var surface = ResolveSurface(mainMenu);
        var onTitle = surface == KitLibMainMenuSurface.MainMenu &&
                      MainMenuCornerButtonHost.IsMainMenuShortcutSurfaceOpen(mainMenu);

        // The multiplayer submenu is the only way into that flow, and every screen it leads to stays in it
        // until the title screen returns, so solo-only entries stay hidden down the chain.
        if (surface == KitLibMainMenuSurface.Multiplayer)
            _inMultiplayerFlow = true;
        else if (onTitle)
            _inMultiplayerFlow = false;

        var registrations = KitLibMainMenuCornerButtonRegistry.GetOrderedButtons(surface);
        if (_inMultiplayerFlow)
            registrations = registrations.Where(static r => !r.SingleplayerOnly).ToArray();
        var signature = BuildSignature(surface, registrations);
        if (!string.Equals(signature, _signature, StringComparison.Ordinal))
            RebuildButtons(mainMenu, registrations, signature);

        // Submenu surfaces host their own entries; the shortcut-surface check only gates the title screen.
        bool surfaceOpen = surface != KitLibMainMenuSurface.MainMenu || onTitle;
        var occupied = surface == KitLibMainMenuSurface.MainMenu
            ? MainMenuCornerButtonHost.FindOccupiedRegistration(mainMenu)
            : null;

        bool show = _railButtons.Count > 0 && (occupied != null || surfaceOpen);

        CaptureInsets(mainMenu);
        // A panel opening is a deliberate request for the rail, so it overrides an earlier Ctrl+B
        // retract; an already-open panel leaves the manual choice alone.
        if (show && !_wantPresence)
            KitLibScreenFrame.SetWrapShown(true, animated: true);

        SetPresence(show, animated: true);
        if (show)
            RefreshButtons(mainMenu, registrations, occupied, surface);
    }

    /// <summary>Surface the rail currently renders for, picked from the type of the open submenu.</summary>
    private static KitLibMainMenuSurface ResolveSurface(NMainMenu mainMenu) =>
        mainMenu.SubmenuStack.Peek() switch {
            NCharacterSelectScreen => KitLibMainMenuSurface.CharacterSelect,
            NMultiplayerSubmenu or NMultiplayerHostSubmenu or NJoinFriendScreen
                or NMultiplayerLoadGameScreen => KitLibMainMenuSurface.Multiplayer,
            NCustomRunScreen => KitLibMainMenuSurface.CustomRun,
            _ => KitLibMainMenuSurface.MainMenu,
        };

    /// <summary>Registration signature of one surface, used to rebuild only when that surface's list changes.</summary>
    private static string BuildSignature(
        KitLibMainMenuSurface surface,
        IReadOnlyList<KitLibMainMenuCornerButtonRegistration> registrations) =>
        $"{(int)surface}:" + string.Join(",", registrations.Select(r => $"{r.ModId}/{r.ButtonId}"));

    /// <summary>
    /// Slide the rail with the frame: its own presence tween, capped by the wrap progress so the rail
    /// and the window edge leave the screen together. Also keeps the contour band and the pushed chrome
    /// in step, and re-bases the main-menu frame inset.
    /// </summary>
    private static void ApplyMenuWrap() {
        float presence = Mathf.Min(_presence, KitLibScreenFrame.WrapT);

        if (_rail != null && GodotObject.IsInstanceValid(_rail)) {
            // Slide fully off screen as the wrap retracts; the rail's right edge lands on x=0 at 0.
            float left = RailColumnLeft - (1f - presence) * (RailColumnLeft + MenuRailButtonWidth);
            _rail.OffsetLeft = left;
            _rail.OffsetRight = left + MenuRailButtonWidth;
        }

        if (_band != null && GodotObject.IsInstanceValid(_band))
            _band.Visible = presence > 0.001f;

        KitLibScreenFrame.SetLeftBand(_band, MenuRailInset * presence, _bandStepY, _bandStepCurve);
        ApplyInsets(presence);

        var menu = _menu;
        if (menu != null && GodotObject.IsInstanceValid(menu))
            KitLibScreenFrame.ApplyMainMenuInsets(menu);
    }

    private static void SetPresence(bool show, bool animated) {
        _wantPresence = show;
        float target = show ? 1f : 0f;

        if (Mathf.IsEqualApprox(_presenceTarget, target)) {
            // Already there or already heading there: refresh the geometry, do not restart the tween.
            ApplyMenuWrap();
            return;
        }

        _presenceTarget = target;
        if (_presenceTween != null && GodotObject.IsInstanceValid(_presenceTween))
            _presenceTween.Kill();
        _presenceTween = null;

        if (!animated || _root == null || !GodotObject.IsInstanceValid(_root)) {
            _presence = target;
            ApplyMenuWrap();
            return;
        }

        var tween = _root.CreateTween();
        tween.TweenMethod(Callable.From<float>(SetPresenceProgress), _presence, target,
                KitLibScreenFrame.AnimationSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.Finished += () => SetPresenceProgress(target);
        _presenceTween = tween;
    }

    private static void SetPresenceProgress(float presence) {
        _presence = presence;
        ApplyMenuWrap();
    }

    private static void RebuildButtons(
        NMainMenu mainMenu,
        IReadOnlyList<KitLibMainMenuCornerButtonRegistration> registrations,
        string signature) {
        if (_buttons == null)
            return;

        foreach (var node in _buttons.GetChildren()) {
            _buttons.RemoveChild(node);
            node.QueueFree();
        }
        _railButtons.Clear();
        _signature = signature;

        if (_indicator != null)
            _indicator.Visible = false;

        foreach (var registration in registrations)
            _buttons.AddChild(CreateButton(mainMenu, registration));
    }

    private static Button CreateButton(NMainMenu mainMenu, KitLibMainMenuCornerButtonRegistration registration) {
        var title = KitLibMainMenuCornerButtonRegistry.ResolveTooltip(registration, I18N.T);
        var button = new Button {
            Name = BuildButtonNodeName(registration),
            Text = title,
            Alignment = HorizontalAlignment.Left,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            CustomMinimumSize = new(MenuRailButtonWidth, KitLibRailChrome.IconButtonSize),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = title,
        };
        if (LabelFont() is { } font)
            button.AddThemeFontOverride("font", font);
        button.AddThemeFontSizeOverride("font_size", (int)LabelFontSize);
        button.AddThemeColorOverride("font_color", LabelColor);
        button.AddThemeColorOverride("font_hover_color", LabelColor);
        button.AddThemeColorOverride("font_pressed_color", LabelColor);
        ApplyButtonStyle(button);

        button.Pressed += () => OnButtonPressed(mainMenu, registration);
        _railButtons.Add(button);
        return button;
    }

    private static Font? LabelFont() {
        if (_labelFontProbed)
            return _labelFont;

        _labelFontProbed = true;
        try {
            _labelFont = ResourceLoader.Load<Font>(LabelFontPath);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib main-menu rail title font failed to load: {ex.Message}");
        }
        if (_labelFont == null)
            MainFile.Logger.Warn($"KitLib main-menu rail title font is missing at {LabelFontPath}.");
        return _labelFont;
    }

    private static void ApplyButtonStyle(Button button) {
        StyleBoxFlat Style(Color background) => new() {
            BgColor = background,
            ContentMarginLeft = (int)ContentPadding,
            CornerRadiusTopLeft = IndicatorRadius,
            CornerRadiusTopRight = IndicatorRadius,
            CornerRadiusBottomLeft = IndicatorRadius,
            CornerRadiusBottomRight = IndicatorRadius,
        };

        button.AddThemeStyleboxOverride("normal", Style(Colors.Transparent));
        button.AddThemeStyleboxOverride("hover", Style(HoverBg));
        button.AddThemeStyleboxOverride("pressed", Style(PressedBg));
        button.AddThemeStyleboxOverride("focus", Style(Colors.Transparent));
        button.AddThemeStyleboxOverride("disabled", Style(Colors.Transparent));
    }

    private static void RefreshButtons(
        NMainMenu mainMenu,
        IReadOnlyList<KitLibMainMenuCornerButtonRegistration> registrations,
        KitLibMainMenuCornerButtonRegistration? occupied,
        KitLibMainMenuSurface surface) {
        var occupiedKey = occupied == null ? null : RegistrationKey(occupied);

        for (var i = 0; i < _railButtons.Count && i < registrations.Count; i++) {
            var registration = registrations[i];
            var button = _railButtons[i];
            button.Visible = MainMenuCornerButtonHost.TryEvaluateButtonVisibility(
                mainMenu, RegistrationKey(registration), surface);

            bool isOpen = occupiedKey != null &&
                string.Equals(RegistrationKey(registration), occupiedKey, StringComparison.OrdinalIgnoreCase);
            button.SetMeta(OpenMetaKey, isOpen);
        }

        SyncIndicator();
    }

    private static void SyncIndicator() {
        if (_indicator == null || !GodotObject.IsInstanceValid(_indicator))
            return;

        Button? open = null;
        foreach (var button in _railButtons) {
            if (button.Visible && button.GetMeta(OpenMetaKey).AsBool()) {
                open = button;
                break;
            }
        }

        if (open == null) {
            _indicator.Visible = false;
            return;
        }

        _indicator.Visible = true;
        _indicator.OffsetTop = open.Position.Y;
        _indicator.OffsetBottom = open.Position.Y + open.Size.Y;
    }

    private static void OnButtonPressed(NMainMenu mainMenu, KitLibMainMenuCornerButtonRegistration registration) {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return;

        try {
            registration.OnPressed(mainMenu);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn(
                $"KitLib main-menu rail button {registration.ModId}/{registration.ButtonId} failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Reserves the rail strip by pushing readable main-menu chrome right, mirroring the in-run inset:
    /// only chrome moves — the background stays at x=0 so the strip has content behind it. The push is
    /// scaled by the rail's presence so the chrome animates back as the strip retracts.
    /// </summary>
    private static void CaptureInsets(NMainMenu mainMenu) {
        CaptureRight(mainMenu, "%MainMenuTextButtons");
        CaptureRight(mainMenu, "%ChangeProfileButton");

        // The widened strip wraps the rail's own entries only: past the last one it tapers back to the
        // frame's own edge, so the spare room below never reads as a wide empty band. The taper scales
        // with the content, and BandStepOverride pins it when a caller wants a fixed height.
        var content = RailContent();
        _bandStepCurve = Mathf.Clamp(content.Height * BandStepCurveRatio, BandStepCurveMin, BandStepCurveMax);

        if (BandStepOverride > 0f)
            _bandStepY = BandStepOverride;
        else if (content.Height < 0f)
            _bandStepY = -1f;
        else
            _bandStepY = content.Bottom + BandStepClearance + _bandStepCurve;
    }

    /// <summary>Screen-space extent of the rail's visible entries; <c>Height</c> is &lt; 0 when it shows none.</summary>
    private static (float Height, float Bottom) RailContent() {
        if (_rail == null || !GodotObject.IsInstanceValid(_rail))
            return (-1f, -1f);

        var visible = 0;
        foreach (var button in _railButtons) {
            if (button.Visible)
                visible++;
        }
        if (visible == 0)
            return (-1f, -1f);

        var height = visible * KitLibRailChrome.IconButtonSize + (visible - 1) * ButtonGap;
        return (height, _rail.GlobalPosition.Y + height);
    }

    /// <summary>Gap between the rail's last entry and the start of the strip's taper back to the frame edge.</summary>
    private const float BandStepClearance = 6f;

    private static void CaptureRight(Node owner, string path) {
        var node = owner.GetNodeOrNull<Control>(path);
        if (node == null || !GodotObject.IsInstanceValid(node) || IsPushed(node))
            return;
        _pushed.Add(new PushedChrome(node, node.OffsetLeft, node.OffsetRight));
    }

    private static bool IsPushed(Control node) =>
        _pushed.Any(entry => entry.Node == node);

    private static void ApplyInsets(float presence) {
        float push = MenuRailInset * presence;
        for (int i = 0; i < _pushed.Count; i++) {
            var entry = _pushed[i];
            if (!GodotObject.IsInstanceValid(entry.Node))
                continue;

            // Game owned the offset when it differs from what we last wrote -> re-base under our push.
            bool gameOwned = !Mathf.IsEqualApprox(entry.Node.OffsetLeft, entry.LastLeft) ||
                             !Mathf.IsEqualApprox(entry.Node.OffsetRight, entry.LastRight);
            if (gameOwned) {
                entry.Left = entry.Node.OffsetLeft - push;
                entry.Right = entry.Node.OffsetRight - push;
            }

            entry.Node.OffsetLeft = entry.Left + push;
            entry.Node.OffsetRight = entry.Right + push;
            entry.LastLeft = entry.Node.OffsetLeft;
            entry.LastRight = entry.Node.OffsetRight;
            _pushed[i] = entry;
        }
    }

    private static void RestoreInset() {
        foreach (var entry in _pushed) {
            if (!GodotObject.IsInstanceValid(entry.Node))
                continue;
            entry.Node.OffsetLeft = entry.Left;
            entry.Node.OffsetRight = entry.Right;
        }
        _pushed.Clear();
    }

    private static void Detach() => ResetStatics();

    private static void ResetStatics() {
        KitLibScreenFrame.RemoveWrapListener(ApplyMenuWrap);
        if (_presenceTween != null && GodotObject.IsInstanceValid(_presenceTween))
            _presenceTween.Kill();
        _presenceTween = null;
        _presence = 0f;
        _presenceTarget = 0f;
        _wantPresence = false;

        RestoreInset();
        _bandStepY = -1f;
        _inMultiplayerFlow = false;
        KitLibScreenFrame.SetLeftBand(0f);
        // The frame is a permanent decoration: restore it so the next scene starts with a full edge.
        KitLibScreenFrame.SetWrapShown(true, animated: false);

        if (_root != null && GodotObject.IsInstanceValid(_root)) {
            _root.GetParent()?.RemoveChild(_root);
            _root.QueueFree();
        }

        _root = null;
        _rail = null;
        _band = null;
        _menu = null;
        _indicator = null;
        _buttons = null;
        _railButtons.Clear();
        _signature = "";
    }

    /// <summary>Mutable record of a pushed chrome node and its resting offsets.</summary>
    private struct PushedChrome {
        public Control Node;
        public float Left;
        public float Right;
        public float LastLeft;
        public float LastRight;

        public PushedChrome(Control node, float left, float right) {
            Node = node;
            Left = left;
            Right = right;
            // Recorded at resting offsets so the first push does not re-base (nodes captured while docked).
            LastLeft = left;
            LastRight = right;
        }
    }

    private static string BuildButtonNodeName(KitLibMainMenuCornerButtonRegistration registration) =>
        $"KitLibRail_{SanitizeNodeToken(registration.ModId)}_{SanitizeNodeToken(registration.ButtonId)}";

    private static string RegistrationKey(KitLibMainMenuCornerButtonRegistration registration) =>
        $"{registration.ModId}/{registration.ButtonId}";

    private static string SanitizeNodeToken(string value) =>
        string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
}
