using Godot;
using KitLib.UI;

namespace KitLib.UI;

internal static class ProgressGuardModSettingsPage {
    internal static Control Build() {
        var stack = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        stack.AddThemeConstantOverride("separation", 8);
        ProgressGuardPanelContent.BuildPanel(stack, ModPanelUI.TryGetOverlayHost());
        return stack;
    }
}
