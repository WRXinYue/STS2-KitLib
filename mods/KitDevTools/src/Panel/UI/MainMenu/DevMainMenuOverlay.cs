using System;
using Godot;

namespace KitLib.UI;

/// <summary>Fullscreen centered modal shell for DevMode panels opened from the title screen.</summary>
internal static class DevMainMenuOverlay {
    internal static (Control Root, VBoxContainer Content) Create(
        Node attachRoot,
        string rootName,
        float panelWidth,
        Action onClose,
        int contentSeparation = 10,
        int zIndex = 2000,
        bool dimBackdrop = true) {
        var (root, content, _) = CreateWithPanel(
            attachRoot, rootName, panelWidth, onClose, contentSeparation, zIndex, dimBackdrop);
        return (root, content);
    }

    internal static (Control Root, VBoxContainer Content, PanelContainer Panel) CreateWithPanel(
        Node attachRoot,
        string rootName,
        float panelWidth,
        Action onClose,
        int contentSeparation = 10,
        int zIndex = 2000,
        bool dimBackdrop = true) {
        Remove(attachRoot, rootName);

        var root = new Control {
            Name = rootName,
            MouseFilter = dimBackdrop ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore,
            ZIndex = dimBackdrop ? zIndex : 0,
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        void CloseOverlay() {
            onClose();
            DevMainMenuUI.NotifyOverlayClosed();
        }

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

        var panel = DevPanelUI.CreateMainMenuModalPanel(panelWidth);
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        root.AddChild(panel);

        var content = panel.GetNode<VBoxContainer>("Content");
        content.AddThemeConstantOverride("separation", contentSeparation);

        attachRoot.AddChild(root);
        DevMainMenuUI.NotifyOverlayOpened(root, panel);
        return (root, content, panel);
    }

    private static Control? FindFirstFocusableDescendant(Control root) {
        if (root.FocusMode != Control.FocusModeEnum.None && root.Visible)
            return root;
        foreach (var child in root.GetChildren()) {
            if (child is not Control c || !c.Visible)
                continue;
            var found = FindFirstFocusableDescendant(c);
            if (found != null)
                return found;
        }
        return null;
    }

    internal static void FocusOverlayContentDeferred(Control overlayRoot, PanelContainer panel) {
        Callable.From(() => {
            if (!GodotObject.IsInstanceValid(overlayRoot) || !GodotObject.IsInstanceValid(panel))
                return;
            overlayRoot.GetViewport()?.GuiReleaseFocus();
            FindFirstFocusableDescendant(panel)?.GrabFocus();
        }).CallDeferred();
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
