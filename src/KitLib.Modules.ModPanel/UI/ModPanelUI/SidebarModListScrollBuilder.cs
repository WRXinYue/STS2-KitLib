using Godot;

namespace KitLib.UI;

/// <summary>Sidebar mod list scroll host (vanilla <see cref="ScrollContainer" /> — same as GitHub main).</summary>
internal static class SidebarModListScrollBuilder {
    public static ScrollContainer Create(out VBoxContainer contentHost) {
        var scroll = new ScrollContainer {
            Name = "ModPanelSidebarModScroll",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = false,
            FocusMode = Control.FocusModeEnum.None,
        };
        contentHost = new VBoxContainer {
            Name = "SidebarScrollInner",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        scroll.AddChild(contentHost);
        return scroll;
    }

    public static void ResetScrollTopDeferred(ScrollContainer scroll) {
        Callable.From(() => {
            if (!GodotObject.IsInstanceValid(scroll))
                return;
            scroll.ScrollVertical = 0;
        }).CallDeferred();
    }
}
