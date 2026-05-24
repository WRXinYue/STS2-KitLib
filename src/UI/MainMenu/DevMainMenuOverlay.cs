using System;
using Godot;

namespace DevMode.UI;

/// <summary>Fullscreen centered modal shell for DevMode panels opened from the title screen.</summary>
internal static class DevMainMenuOverlay {
    internal static (Control Root, VBoxContainer Content) Create(
        Node attachRoot,
        string rootName,
        float panelWidth,
        Action onClose,
        int contentSeparation = 10,
        int zIndex = 2000) {
        Remove(attachRoot, rootName);

        var root = new Control {
            Name = rootName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = zIndex,
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new ColorRect {
            Color = new Color(0, 0, 0, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += e => {
            if (e is InputEventMouseButton { Pressed: true })
                onClose();
        };
        root.AddChild(backdrop);

        var panel = DevPanelUI.CreateMainMenuModalPanel(panelWidth);
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        root.AddChild(panel);

        var content = panel.GetNode<VBoxContainer>("Content");
        content.AddThemeConstantOverride("separation", contentSeparation);

        attachRoot.AddChild(root);
        return (root, content);
    }

    internal static void Remove(Node attachRoot, string rootName) =>
        attachRoot.GetNodeOrNull<Control>(rootName)?.QueueFree();

    internal static void RemoveAnywhere(string rootName) {
        var tree = Engine.GetMainLoop() as SceneTree;
        tree?.Root.FindChild(rootName, recursive: true, owned: false)?.QueueFree();
    }
}
