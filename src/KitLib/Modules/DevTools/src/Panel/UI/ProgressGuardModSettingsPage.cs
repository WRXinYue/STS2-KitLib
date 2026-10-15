using Godot;

namespace KitLib.UI;

internal static class ProgressGuardModSettingsPage {
    internal static Control Build(Node? overlayHost = null) {
        var stack = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        stack.AddThemeConstantOverride("separation", 8);
        ProgressGuardPanelContent.BuildPanel(stack, overlayHost);
        return stack;
    }
}
