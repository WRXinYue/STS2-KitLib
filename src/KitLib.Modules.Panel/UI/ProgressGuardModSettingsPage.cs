using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class ProgressGuardModSettingsPage {
    internal static Control Build() {
        var stack = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        stack.AddThemeConstantOverride("separation", 8);
        ProgressGuardPanelContent.BuildPanel(stack, ResolveMainMenu());
        return stack;
    }

    static NMainMenu? ResolveMainMenu() {
        var host = ModPanelUI.TryGetHostMainMenu();
        if (host != null)
            return host;
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
            return null;
        return FindMainMenu(tree.Root);
    }

    static NMainMenu? FindMainMenu(Node node) {
        if (node is NMainMenu mainMenu)
            return mainMenu;
        foreach (var child in node.GetChildren()) {
            var found = FindMainMenu(child);
            if (found != null)
                return found;
        }
        return null;
    }
}
