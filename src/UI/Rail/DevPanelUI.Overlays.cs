using KitLib;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    private const string SettingsRootName = "KitLibSettings";
    private const string AiRootName = "KitLibAi";
    private const string CheatsRootName = "KitLibCheats";
    private const string SaveLoadRootName = "KitLibSaveLoad";
    private const string SaveLoadMenuHostName = "SaveLoadMenuHost";
    private const string SaveLoadExtensionWidthKey = "KitLibSaveLoad_ext";
    private const string RestartSeedRootName = "KitLibRestartSeed";

    private static (Control root, PanelContainer panel, VBoxContainer vbox) CreateOverlayRoot(
        NGlobalUi globalUi, string rootName, float panelWidth = 0f, int contentSeparation = 10) {
        var (root, panel, vbox) = CreateBrowserOverlayShell(
            globalUi,
            rootName,
            panelWidth,
            () => ((Node)globalUi).GetNodeOrNull<Control>(rootName)?.QueueFree(),
            contentSeparation);
        return (root, panel, vbox);
    }

    private static void AddBrowserNavTab(VBoxContainer vbox, string title) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        var tab = new Button { Text = title, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 32) };
        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 4,
            ContentMarginBottom = 6
        };
        foreach (var s in new[] { "normal", "hover", "pressed", "focus" })
            tab.AddThemeStyleboxOverride(s, flat);
        tab.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        tab.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(tab);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        vbox.AddChild(row);
        vbox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.Separator,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });
    }
}
