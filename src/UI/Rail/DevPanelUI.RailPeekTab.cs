using System;
using KitLib.Icons;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    private enum PeekTabMode {
        Normal,
        Intro,
        LogWarn,
        LogError
    }

    private static StyleBoxFlat? _peekTabStyle;
    private static Button? _peekTabBtn;
    private static Tween? _peekTabTween;
    private static PeekTabMode _peekTabMode = PeekTabMode.Normal;

    internal static bool IsPeekTabVisible
        => _peekTabBtn != null
           && GodotObject.IsInstanceValid(_peekTabBtn)
           && _peekTabBtn.Visible;

    internal static bool IsPeekTabAnimating()
        => _peekTabTween != null && GodotObject.IsInstanceValid(_peekTabTween);

    internal static void CreatePeekTab(Control root) {
        var peekTab = new Button {
            Name = "RailPeekTab",
            CustomMinimumSize = new Vector2(14, 48),
            AnchorLeft = 0,
            AnchorRight = 0,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = 0,
            OffsetRight = 14,
            OffsetTop = -24,
            OffsetBottom = 24,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            IconAlignment = HorizontalAlignment.Center,
            Icon = MdiIcon.ChevronRight.Texture(12, ColIconNormal),
            Visible = true
        };

        _peekTabStyle = new StyleBoxFlat {
            BgColor = new Color(ColRailBg.R, ColRailBg.G, ColRailBg.B, 0.6f),
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8
        };
        peekTab.AddThemeStyleboxOverride("normal", _peekTabStyle);
        peekTab.AddThemeStyleboxOverride("hover", _peekTabStyle);
        peekTab.AddThemeStyleboxOverride("pressed", _peekTabStyle);
        peekTab.AddThemeStyleboxOverride("focus", _peekTabStyle);
        _peekTabBtn = peekTab;
        root.AddChild(peekTab);
    }

    internal static void WirePeekTabPressed(Action onPressed)
        => _peekTabBtn!.Pressed += onPressed;

    internal static void SetPeekTabVisible(bool visible) {
        if (_peekTabBtn != null && GodotObject.IsInstanceValid(_peekTabBtn))
            _peekTabBtn.Visible = visible;
    }

    internal static void ApplyPeekTabTheme() {
        if (_peekTabStyle != null)
            _peekTabStyle.BgColor = new Color(ColRailBg.R, ColRailBg.G, ColRailBg.B, 0.6f);
        if (_peekTabBtn != null && GodotObject.IsInstanceValid(_peekTabBtn) && !IsPeekTabAnimating())
            _peekTabBtn.Icon = MdiIcon.ChevronRight.Texture(12, ColIconNormal);
    }

    internal static void RefreshPeekTabPresentation() {
        if (!IsPeekTabVisible) {
            StopPeekTabPresentation();
            return;
        }

        var mode = ResolvePeekTabMode();
        if (mode == PeekTabMode.Normal) {
            StopPeekTabPresentation();
            return;
        }

        if (_peekTabTween != null && GodotObject.IsInstanceValid(_peekTabTween) && _peekTabMode == mode)
            return;

        StopPeekTabPresentation();
        StartPeekTabPresentation(mode);
    }

    internal static void StopPeekTabPresentation() {
        _peekTabTween?.Kill();
        _peekTabTween = null;
        _peekTabMode = PeekTabMode.Normal;
        ResetPeekTabChrome();
    }

    internal static void TeardownPeekTab() {
        StopPeekTabPresentation();
        _peekTabStyle = null;
        _peekTabBtn = null;
    }

    private static PeekTabMode ResolvePeekTabMode() {
        if (!IsPeekTabVisible)
            return PeekTabMode.Normal;

        var severity = LogCollector.UnseenAlertSeverity;
        if (severity >= LogLevel.Error)
            return PeekTabMode.LogError;
        if (severity >= LogLevel.Warn)
            return PeekTabMode.LogWarn;
        if (SettingsStore.ShouldShowRailIntroHint())
            return PeekTabMode.Intro;
        return PeekTabMode.Normal;
    }

    private static void ResetPeekTabChrome() {
        if (_peekTabStyle != null) {
            _peekTabStyle.BorderWidthLeft = 0;
            _peekTabStyle.BorderWidthTop = 0;
            _peekTabStyle.BorderWidthRight = 0;
            _peekTabStyle.BorderWidthBottom = 0;
            _peekTabStyle.ShadowSize = 0;
            _peekTabStyle.BgColor = new Color(ColRailBg.R, ColRailBg.G, ColRailBg.B, 0.6f);
        }

        if (_peekTabBtn != null && GodotObject.IsInstanceValid(_peekTabBtn)) {
            _peekTabBtn.Modulate = Colors.White;
            _peekTabBtn.Icon = MdiIcon.ChevronRight.Texture(12, ColIconNormal);
        }
    }

    private static void StartPeekTabPresentation(PeekTabMode mode) {
        if (_peekTabBtn == null || _peekTabStyle == null)
            return;

        _peekTabMode = mode;

        Color borderDim;
        Color borderBright;
        float halfCycle;
        float minAlpha;

        switch (mode) {
        case PeekTabMode.LogError:
            borderDim = new Color(1f, 0.37f, 0.37f, 0.35f);
            borderBright = new Color(1f, 0.37f, 0.37f);
            halfCycle = 0.35f;
            minAlpha = 0.5f;
            break;
        case PeekTabMode.LogWarn:
            borderDim = new Color(1f, 0.78f, 0.25f, 0.35f);
            borderBright = new Color(1f, 0.78f, 0.25f);
            halfCycle = 0.65f;
            minAlpha = 0.5f;
            break;
        default:
            var accent = KitLibTheme.Accent;
            borderDim = new Color(accent.R, accent.G, accent.B, 0.25f);
            borderBright = new Color(accent.R, accent.G, accent.B, 1f);
            halfCycle = 0.55f;
            minAlpha = 0.45f;
            break;
        }

        var bgBase = new Color(ColRailBg.R, ColRailBg.G, ColRailBg.B, 0.6f);
        var bgBright = new Color(ColRailBg.R, ColRailBg.G, ColRailBg.B, 0.92f);
        var shadowDim = new Color(borderBright.R, borderBright.G, borderBright.B, mode == PeekTabMode.Intro ? 0.08f : 0.12f);
        var shadowBright = new Color(borderBright.R, borderBright.G, borderBright.B, mode == PeekTabMode.Intro ? 0.55f : 0.55f);

        _peekTabStyle.BorderWidthLeft = 1;
        _peekTabStyle.BorderWidthTop = 1;
        _peekTabStyle.BorderWidthRight = 1;
        _peekTabStyle.BorderWidthBottom = 1;
        _peekTabStyle.BorderColor = borderDim;
        _peekTabStyle.ShadowColor = shadowDim;
        _peekTabStyle.ShadowSize = 2;
        _peekTabBtn.Modulate = Colors.White;
        _peekTabBtn.Icon = MdiIcon.ChevronRight.Texture(12, borderDim);

        _peekTabTween = _peekTabBtn.CreateTween();
        _peekTabTween.SetLoops();
        _peekTabTween.SetTrans(Tween.TransitionType.Sine);
        _peekTabTween.SetEase(Tween.EaseType.InOut);

        _peekTabTween.TweenMethod(Callable.From((float t) => {
            if (_peekTabBtn == null || !GodotObject.IsInstanceValid(_peekTabBtn))
                return;
            var iconCol = borderDim.Lerp(borderBright, t);
            _peekTabBtn.Icon = MdiIcon.ChevronRight.Texture(12, iconCol);
            if (_peekTabStyle != null)
                _peekTabStyle.BorderColor = iconCol;
        }), 0f, 1f, halfCycle);
        _peekTabTween.Parallel()
            .TweenProperty(_peekTabBtn, "modulate:a", minAlpha, halfCycle);
        _peekTabTween.Parallel()
            .TweenProperty(_peekTabStyle, "bg_color", bgBright, halfCycle);
        _peekTabTween.Parallel()
            .TweenProperty(_peekTabStyle, "shadow_color", shadowBright, halfCycle);
        _peekTabTween.Parallel()
            .TweenProperty(_peekTabStyle, "shadow_size", 6, halfCycle);

        _peekTabTween.TweenMethod(Callable.From((float t) => {
            if (_peekTabBtn == null || !GodotObject.IsInstanceValid(_peekTabBtn))
                return;
            var iconCol = borderBright.Lerp(borderDim, t);
            _peekTabBtn.Icon = MdiIcon.ChevronRight.Texture(12, iconCol);
            if (_peekTabStyle != null)
                _peekTabStyle.BorderColor = iconCol;
        }), 0f, 1f, halfCycle);
        _peekTabTween.Parallel()
            .TweenProperty(_peekTabBtn, "modulate:a", 1f, halfCycle);
        _peekTabTween.Parallel()
            .TweenProperty(_peekTabStyle, "bg_color", bgBase, halfCycle);
        _peekTabTween.Parallel()
            .TweenProperty(_peekTabStyle, "shadow_color", shadowDim, halfCycle);
        _peekTabTween.Parallel()
            .TweenProperty(_peekTabStyle, "shadow_size", 2, halfCycle);
    }
}
