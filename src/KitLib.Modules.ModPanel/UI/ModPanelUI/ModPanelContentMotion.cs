using Godot;

namespace KitLib.UI;

internal static class ModPanelContentMotion {
    private const string SkeletonName = "ModPanelContentSkeleton";
    private const string SkeletonPulseMeta = "modpanel_sk_pulse";
    private const string ContentScrollMeta = "modpanel_content_scroll";
    private const float MinHeightHoldMin = 120f;

    private static int _refreshGeneration;
    private static float _heldContentMinHeight;

    internal static int BeginRefresh(VBoxContainer list) {
        _refreshGeneration++;
        HoldContentMinHeight(list);
        ClearContainerChildren(list);
        ResetContentScroll(list);
        ScheduleSkeleton(list, _refreshGeneration);
        return _refreshGeneration;
    }

    internal static void Present(VBoxContainer list, int generation, Control content) {
        if (!GodotObject.IsInstanceValid(content)) {
            CancelGeneration(generation);
            return;
        }
        if (generation != _refreshGeneration) {
            content.QueueFree();
            return;
        }
        ClearContainerChildren(list);
        list.AddChild(content);
        ResetContentScroll(list);
        ReleaseContentMinHeightDeferred(list);
    }

    internal static void CancelGeneration(int generation) {
        if (generation == _refreshGeneration) {
            _refreshGeneration++;
            ReleaseContentMinHeightDeferred(null);
        }
    }

    private static void HoldContentMinHeight(VBoxContainer list) {
        var h = list.GetCombinedMinimumSize().Y;
        if (h > 0f)
            _heldContentMinHeight = Mathf.Max(_heldContentMinHeight, h);
        if (_heldContentMinHeight >= MinHeightHoldMin)
            list.CustomMinimumSize = new Vector2(0f, _heldContentMinHeight);
    }

    private static void ReleaseContentMinHeightDeferred(VBoxContainer? list) {
        Callable.From(() => {
            _heldContentMinHeight = 0f;
            if (list != null && GodotObject.IsInstanceValid(list))
                list.CustomMinimumSize = Vector2.Zero;
        }).CallDeferred();
    }

    private static void ResetContentScroll(VBoxContainer list) {
        var scroll = FindContentScroll(list);
        if (scroll == null)
            return;
        scroll.ScrollVertical = 0;
        scroll.ScrollHorizontal = 0;
    }

    private static ScrollContainer? FindContentScroll(Node node) {
        Node? current = node;
        while (current != null) {
            if (current is ScrollContainer scroll && scroll.HasMeta(ContentScrollMeta))
                return scroll;
            current = current.GetParent();
        }
        return null;
    }

    private static void ScheduleSkeleton(VBoxContainer list, int generation) {
        var tree = list.GetTree();
        if (tree == null)
            return;
        var timer = tree.CreateTimer(ModPanelUiMotion.SkeletonDelaySec);
        timer.Timeout += () => {
            if (generation != _refreshGeneration)
                return;
            if (!GodotObject.IsInstanceValid(list))
                return;
            if (list.GetChildCount() > 0)
                return;
            list.AddChild(CreateSkeleton());
        };
    }

    private static Control CreateSkeleton() {
        var root = new VBoxContainer {
            Name = SkeletonName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 10);

        var accent = ModPanelUiPalette.SidebarModActiveAccent;
        var barFill = new Color(accent.R, accent.G, accent.B, 0.12f);
        var barBorder = new Color(accent.R, accent.G, accent.B, 0.22f);
        float[] heights = [22f, 40f, 40f, 28f];
        foreach (var h in heights) {
            var bar = new PanelContainer {
                CustomMinimumSize = new Vector2(0f, h),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            bar.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
                BgColor = barFill,
                BorderColor = barBorder,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 6,
                ContentMarginBottom = 6,
            });
            root.AddChild(bar);
        }

        root.Modulate = new Color(1f, 1f, 1f, 0.55f);
        var tw = root.CreateTween();
        root.SetMeta(SkeletonPulseMeta, tw);
        tw.SetLoops();
        tw.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.TweenProperty(root, "modulate:a", 0.92f, 0.55f);
        tw.TweenProperty(root, "modulate:a", 0.55f, 0.55f);
        return root;
    }

    private static void ClearContainerChildren(Node container) {
        while (container.GetChildCount() > 0) {
            var c = container.GetChild(0);
            if (c is Control ctrl)
                ModPanelUiMotion.KillTween(ctrl, SkeletonPulseMeta);
            container.RemoveChild(c);
            c.QueueFree();
        }
    }
}
