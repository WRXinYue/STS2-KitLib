using System;
using Godot;

namespace KitLib.UI;

/// <summary>Fullscreen centered modal shell for DevMode panels opened from the title screen.</summary>
internal static class DevMainMenuOverlay {
    internal static (Control Root, VBoxContainer Content, PanelContainer Panel) CreateWithPanel(
        Node attachRoot,
        string rootName,
        float panelWidth,
        Action onClose,
        int contentSeparation = 10,
        int zIndex = 2000,
        bool dimBackdrop = true,
        bool dockLeft = false) {
        Remove(attachRoot, rootName);

        var root = new Control {
            Name = rootName,
            MouseFilter = dimBackdrop ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore,
            ZIndex = dimBackdrop ? zIndex : 0,
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        void CloseOverlay() => onClose();

        if (dimBackdrop) {
            var backdrop = new ColorRect {
                Color = new Color(0, 0, 0, 0.75f),
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            backdrop.GuiInput += e => {
                if (e is InputEventMouseButton { Pressed: true })
                    Callable.From(CloseOverlay).CallDeferred();
            };
            root.AddChild(backdrop);
        }

        var panel = DevPanelUI.CreateMainMenuModalPanel(panelWidth, dockLeft);
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        root.AddChild(panel);

        var content = panel.GetNode<VBoxContainer>("Content");
        content.AddThemeConstantOverride("separation", contentSeparation);

        attachRoot.AddChild(root);
        return (root, content, panel);
    }

    /// <summary>Whether an overlay registered under <paramref name="rootName"/> is currently on screen.</summary>
    internal static bool IsPresent(string rootName) {
        var tree = Engine.GetMainLoop() as SceneTree;
        var root = tree?.Root;
        if (root == null)
            return false;

        foreach (var node in root.FindChildren(rootName, recursive: true, owned: false))
            return !node.IsQueuedForDeletion();
        return false;
    }

    internal static void Remove(Node attachRoot, string rootName) =>
        RemoveAnywhere(rootName);

    internal static void RemoveAnywhere(string rootName) {
        var tree = Engine.GetMainLoop() as SceneTree;
        var root = tree?.Root;
        if (root == null)
            return;

        // QueueFree is deferred; never loop FindChild — the node stays in-tree until end of frame.
        foreach (var node in root.FindChildren(rootName, recursive: true, owned: false))
            node.QueueFree();
    }
}
