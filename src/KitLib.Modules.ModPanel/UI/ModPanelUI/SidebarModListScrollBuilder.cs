using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace KitLib.UI;

/// <summary>Builds a vanilla-style <see cref="NScrollableContainer" /> for the sidebar mod list.</summary>
internal static class SidebarModListScrollBuilder {
    public static NScrollableContainer Create(out VBoxContainer contentHost) {
        var scroll = new NScrollableContainer {
            Name = "ModPanelSidebarModScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        scroll.GrowHorizontal = Control.GrowDirection.Both;
        scroll.GrowVertical = Control.GrowDirection.Both;

        var mask = new ColorRect {
            Name = "Mask",
            Color = Colors.Transparent,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipChildren = CanvasItem.ClipChildrenMode.Only,
        };
        mask.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mask.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        mask.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.AddChild(mask);

        contentHost = new VBoxContainer {
            Name = "Content",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        mask.AddChild(contentHost);

        var scrollbar = PreloadManager.Cache.GetScene(SceneHelper.GetScenePath("ui/scrollbar"))
            .Instantiate<NScrollbar>(PackedScene.GenEditState.Disabled);
        scrollbar.Name = "Scrollbar";
        scrollbar.AnchorLeft = 1f;
        scrollbar.AnchorTop = 0f;
        scrollbar.AnchorRight = 1f;
        scrollbar.AnchorBottom = 1f;
        scrollbar.OffsetLeft = 6f;
        scrollbar.OffsetTop = -8f;
        scrollbar.OffsetRight = 54f;
        scrollbar.OffsetBottom = -8f;
        scrollbar.GrowVertical = Control.GrowDirection.Both;
        scroll.AddChild(scrollbar);

        Callable.From(scroll.DisableScrollingIfContentFits).CallDeferred();
        return scroll;
    }
}
