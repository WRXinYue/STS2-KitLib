using Godot;

namespace KitLib.UI;

internal static class ModPanelUiMotion {
    internal const float FadeInDuration = 0.18f;
    internal const float SidebarTransitionDuration = 0.15f;

    internal const string FadeTweenMetaKey = "modpanel_fade_tw";

    internal static void FadeIn(Control node, float duration = FadeInDuration, System.Action? onComplete = null) {
        if (!GodotObject.IsInstanceValid(node))
            return;
        KillTween(node, FadeTweenMetaKey);
        node.Modulate = new Color(1f, 1f, 1f, 0f);
        Callable.From(() => {
            if (!GodotObject.IsInstanceValid(node))
                return;
            var tw = node.CreateTween();
            node.SetMeta(FadeTweenMetaKey, tw);
            tw.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(node, "modulate:a", 1f, duration);
            tw.TweenCallback(Callable.From(() => {
                if (!GodotObject.IsInstanceValid(node))
                    return;
                node.Modulate = Colors.White;
                if (node.HasMeta(FadeTweenMetaKey))
                    node.RemoveMeta(FadeTweenMetaKey);
                onComplete?.Invoke();
            }));
        }).CallDeferred();
    }

    internal static void KillTween(Control? node, string metaKey) {
        if (node == null || !GodotObject.IsInstanceValid(node) || !node.HasMeta(metaKey))
            return;
        var v = node.GetMeta(metaKey);
        if (v.VariantType == Variant.Type.Object && v.AsGodotObject() is Tween oldTw)
            oldTw.Kill();
        node.RemoveMeta(metaKey);
    }
}
