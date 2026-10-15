using Godot;

namespace KitLib.UI;

internal static class ModPanelSidebarMotion {
    private const string RowTweenMeta = "modpanel_row_tw";
    private const string SelectedMeta = "modpanel_row_selected";

    internal readonly record struct SidebarRowVisual(
        Color BgColor,
        Color BorderColor,
        float BorderWidthLeft,
        float BorderWidthBottom);

    internal static void ApplyRowStyle(StyleBoxFlat box, Control tweenHost, bool selected, bool pressed,
        bool focused) {
        ModPanelUiMotion.KillTween(tweenHost, RowTweenMeta);
        ModPanelUI.ApplySidebarModGroupInnerRowStyle(box, selected, pressed, focused);
        box.SetMeta(SelectedMeta, selected);
    }

    internal static void AnimateRowStyle(StyleBoxFlat box, Control tweenHost, bool selected, bool pressed,
        bool focused) {
        if (pressed || focused) {
            ApplyRowStyle(box, tweenHost, selected, pressed, focused);
            return;
        }

        var from = CaptureVisual(box);
        ModPanelUI.ApplySidebarModGroupInnerRowStyle(box, selected, pressed, focused);
        var to = CaptureVisual(box);

        var wasSelected = box.HasMeta(SelectedMeta) && box.GetMeta(SelectedMeta).AsBool();
        box.SetMeta(SelectedMeta, selected);
        if (wasSelected == selected && from.BorderWidthLeft == to.BorderWidthLeft
            && from.BorderWidthBottom == to.BorderWidthBottom) {
            box.BgColor = to.BgColor;
            box.BorderColor = to.BorderColor;
            box.BorderWidthLeft = (int)to.BorderWidthLeft;
            box.BorderWidthBottom = (int)to.BorderWidthBottom;
            return;
        }

        box.BgColor = from.BgColor;
        box.BorderColor = from.BorderColor;
        box.BorderWidthLeft = (int)from.BorderWidthLeft;
        box.BorderWidthBottom = (int)from.BorderWidthBottom;

        ModPanelUiMotion.KillTween(tweenHost, RowTweenMeta);
        var tw = tweenHost.CreateTween();
        tweenHost.SetMeta(RowTweenMeta, tw);
        tw.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenMethod(Callable.From<float>(weight => {
            if (!GodotObject.IsInstanceValid(box))
                return;
            var w = Mathf.Clamp(weight, 0f, 1f);
            box.BgColor = from.BgColor.Lerp(to.BgColor, w);
            box.BorderColor = from.BorderColor.Lerp(to.BorderColor, w);
            box.BorderWidthLeft = (int)Mathf.Lerp(from.BorderWidthLeft, to.BorderWidthLeft, w);
            box.BorderWidthBottom = (int)Mathf.Lerp(from.BorderWidthBottom, to.BorderWidthBottom, w);
        }), 0.0, 1.0, ModPanelUiMotion.SidebarTransitionDuration);
        tw.TweenCallback(Callable.From(() => {
            if (GodotObject.IsInstanceValid(tweenHost) && tweenHost.HasMeta(RowTweenMeta))
                tweenHost.RemoveMeta(RowTweenMeta);
        }));
    }

    private static SidebarRowVisual CaptureVisual(StyleBoxFlat box) => new(
        box.BgColor,
        box.BorderColor,
        box.BorderWidthLeft,
        box.BorderWidthBottom);
}
