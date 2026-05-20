using DevMode.Icons;
using DevMode.Settings;
using Godot;

namespace DevMode.UI;

internal static partial class DevPanelUI {
    private static Tween? _peekTabBlinkTween;

    /// <summary>Blinks the collapsed-rail peek chevron while first-run intro hint is active.</summary>
    internal static void RefreshRailIntroHints() {
        bool shouldBlink = SettingsStore.ShouldShowRailIntroHint()
            && _peekTabBtn != null
            && GodotObject.IsInstanceValid(_peekTabBtn)
            && _peekTabBtn.Visible;

        if (!shouldBlink) {
            StopAllRailBlinkHints();
            return;
        }

        // Poll timer runs often; do not restart an already-running tween.
        if (_peekTabBlinkTween != null && GodotObject.IsInstanceValid(_peekTabBlinkTween))
            return;

        StartPeekTabBlink();
    }

    internal static void StopAllRailBlinkHints() {
        _peekTabBlinkTween?.Kill();
        _peekTabBlinkTween = null;

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

    private static void StartPeekTabBlink() {
        if (_peekTabBtn == null || _peekTabStyle == null)
            return;

        _peekTabBlinkTween?.Kill();

        var accent = DevModeTheme.Accent;
        var bgBase = new Color(ColRailBg.R, ColRailBg.G, ColRailBg.B, 0.6f);
        var bgBright = new Color(ColRailBg.R, ColRailBg.G, ColRailBg.B, 0.92f);
        var borderDim = new Color(accent.R, accent.G, accent.B, 0.25f);
        var borderBright = new Color(accent.R, accent.G, accent.B, 1f);
        var shadowDim = new Color(accent.R, accent.G, accent.B, 0.08f);
        var shadowBright = new Color(accent.R, accent.G, accent.B, 0.55f);

        _peekTabStyle.BorderWidthLeft = 1;
        _peekTabStyle.BorderWidthTop = 1;
        _peekTabStyle.BorderWidthRight = 1;
        _peekTabStyle.BorderWidthBottom = 1;
        _peekTabStyle.BorderColor = borderDim;
        _peekTabStyle.ShadowColor = shadowDim;
        _peekTabStyle.ShadowSize = 2;

        _peekTabBlinkTween = _peekTabBtn.CreateTween();
        _peekTabBlinkTween.SetLoops();
        _peekTabBlinkTween.SetTrans(Tween.TransitionType.Sine);
        _peekTabBlinkTween.SetEase(Tween.EaseType.InOut);

        _peekTabBtn.Icon = MdiIcon.ChevronRight.Texture(12, borderDim);

        _peekTabBlinkTween.TweenMethod(Callable.From((float t) => {
            if (_peekTabBtn == null || !GodotObject.IsInstanceValid(_peekTabBtn))
                return;
            var iconCol = borderDim.Lerp(borderBright, t);
            _peekTabBtn.Icon = MdiIcon.ChevronRight.Texture(12, iconCol);
        }), 0f, 1f, 0.55f);
        _peekTabBlinkTween.Parallel()
            .TweenProperty(_peekTabBtn, "modulate:a", 0.45f, 0.55f);
        _peekTabBlinkTween.Parallel()
            .TweenProperty(_peekTabStyle, "bg_color", bgBright, 0.55f);
        _peekTabBlinkTween.Parallel()
            .TweenProperty(_peekTabStyle, "border_color", borderBright, 0.55f);
        _peekTabBlinkTween.Parallel()
            .TweenProperty(_peekTabStyle, "shadow_color", shadowBright, 0.55f);
        _peekTabBlinkTween.Parallel()
            .TweenProperty(_peekTabStyle, "shadow_size", 6, 0.55f);

        _peekTabBlinkTween.TweenMethod(Callable.From((float t) => {
            if (_peekTabBtn == null || !GodotObject.IsInstanceValid(_peekTabBtn))
                return;
            var iconCol = borderBright.Lerp(borderDim, t);
            _peekTabBtn.Icon = MdiIcon.ChevronRight.Texture(12, iconCol);
        }), 0f, 1f, 0.55f);
        _peekTabBlinkTween.Parallel()
            .TweenProperty(_peekTabBtn, "modulate:a", 1f, 0.55f);
        _peekTabBlinkTween.Parallel()
            .TweenProperty(_peekTabStyle, "bg_color", bgBase, 0.55f);
        _peekTabBlinkTween.Parallel()
            .TweenProperty(_peekTabStyle, "border_color", borderDim, 0.55f);
        _peekTabBlinkTween.Parallel()
            .TweenProperty(_peekTabStyle, "shadow_color", shadowDim, 0.55f);
        _peekTabBlinkTween.Parallel()
            .TweenProperty(_peekTabStyle, "shadow_size", 2, 0.55f);
    }
}
