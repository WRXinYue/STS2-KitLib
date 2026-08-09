using Godot;

namespace KitLib.UI;

internal static class ModPanelContentMotion {
    private const string ContentScrollMeta = "modpanel_content_scroll";
    private const float MinHeightHoldMin = 120f;

    private static int _refreshGeneration;
    private static float _heldContentMinHeight;

    internal static int BeginRefresh(VBoxContainer list) {
        _refreshGeneration++;
        HoldContentMinHeight(list);
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
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        content.Modulate = Colors.White;
        list.AddChild(content);
        ResetContentScroll(list);
        ReleaseContentMinHeight(list);
    }

    internal static void CancelGeneration(int generation) {
        if (generation == _refreshGeneration) {
            _refreshGeneration++;
            ReleaseContentMinHeight(null);
        }
    }

    private static void HoldContentMinHeight(VBoxContainer list) {
        var h = list.GetCombinedMinimumSize().Y;
        if (h > 0f)
            _heldContentMinHeight = Mathf.Max(_heldContentMinHeight, h);
        if (_heldContentMinHeight >= MinHeightHoldMin)
            list.CustomMinimumSize = new Vector2(0f, _heldContentMinHeight);
    }

    private static void ReleaseContentMinHeight(VBoxContainer? list) {
        _heldContentMinHeight = 0f;
        if (list != null && GodotObject.IsInstanceValid(list))
            list.CustomMinimumSize = Vector2.Zero;
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

    private static void ClearContainerChildren(Node container) {
        while (container.GetChildCount() > 0) {
            var c = container.GetChild(0);
            container.RemoveChild(c);
            c.QueueFree();
        }
    }
}
