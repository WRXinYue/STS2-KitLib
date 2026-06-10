using Godot;

namespace KitLib.UI;

internal static class ModPanelUiMotion {
    internal const float FadeInDuration = 0.16f;
    internal const float SidebarTransitionDuration = 0.15f;
    internal const float SkeletonDelaySec = 0.10f;

    private const string FadeTweenMeta = "modpanel_fade_tw";

    internal static void FadeIn(Control node, float duration = FadeInDuration) {
        if (!GodotObject.IsInstanceValid(node))
            return;
        KillTween(node, FadeTweenMeta);
        node.Modulate = new Color(1f, 1f, 1f, 0f);
        var tw = node.CreateTween();
        node.SetMeta(FadeTweenMeta, tw);
        tw.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(node, "modulate:a", 1f, duration);
        tw.TweenCallback(Callable.From(() => {
            if (GodotObject.IsInstanceValid(node) && node.HasMeta(FadeTweenMeta))
                node.RemoveMeta(FadeTweenMeta);
        }));
    }

    internal static void KillTween(Control node, string metaKey) {
        if (!node.HasMeta(metaKey))
            return;
        var v = node.GetMeta(metaKey);
        if (v.VariantType == Variant.Type.Object && v.AsGodotObject() is Tween oldTw)
            oldTw.Kill();
        node.RemoveMeta(metaKey);
    }
}
