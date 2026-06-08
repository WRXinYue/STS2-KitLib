using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>Command reference — spliced to the DevMode rail, matching card / relic browser layout.</summary>
internal static class ConsoleUI {
    private const string RootName = "KitLibConsole";
    private const float PanelW = 580f;
    private static readonly ConsoleBridge _bridge = new();

    public static void Show(NGlobalUi globalUi) {
        Remove(globalUi);

        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, PanelW, () => Remove(globalUi));

        // ── Nav tab ──
        BuildNavTab(vbox);

        // ── Search ──
        var (searchRow, searchInput) = DevPanelUI.CreateSearchRow(I18N.T("console.search", "Filter commands..."));
        vbox.AddChild(searchRow);

        // ── Scrollable command list ──
        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var listBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        listBox.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(listBox);
        vbox.AddChild(scroll);

        // ── Copy hint ──
        var hint = new Label {
            Text = I18N.T("console.copyHint", "Click a command to copy to clipboard"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color(0.38f, 0.38f, 0.45f));
        vbox.AddChild(hint);

        PopulateCommands(listBox, "");
        searchInput.TextChanged += filter => PopulateCommands(listBox, filter);

        ((Node)globalUi).AddChild(root);
        searchInput.GrabFocus();
    }

    public static void Remove(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
    }

    private static void BuildNavTab(VBoxContainer vbox) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        var tab = new Button {
            Text = I18N.T("console.title", "Command Reference"),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 32)
        };
        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 4,
            ContentMarginBottom = 6
        };
        foreach (var s in new[] { "normal", "hover", "pressed", "focus" }) tab.AddThemeStyleboxOverride(s, flat);
        tab.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        tab.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(tab);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        vbox.AddChild(row);
        vbox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.ButtonBgNormal,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });
    }

    private static void PopulateCommands(VBoxContainer listBox, string filter) {
        foreach (var child in listBox.GetChildren())
            if (child is Node n) n.QueueFree();

        if (!_bridge.TryGetCommands(out var commands, out _))
            return;

        var filtered = string.IsNullOrWhiteSpace(filter)
            ? commands
            : commands.Where(c =>
                c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        var native = filtered.Where(c => c.IsOfficial).ToList();
        var devmode = filtered.Where(c => !c.IsOfficial).ToList();

        if (native.Count > 0) {
            listBox.AddChild(DevPanelUI.CreateSectionHeader(
                $"{I18N.T("console.section.native", "Native Commands")}  ({native.Count})"));
            foreach (var cmd in native)
                listBox.AddChild(CreateCommandEntry(cmd));
        }

        if (devmode.Count > 0) {
            listBox.AddChild(DevPanelUI.CreateSectionHeader(
                $"{I18N.T("console.section.devmode", "KitLib Commands")}  ({devmode.Count})"));
            foreach (var cmd in devmode)
                listBox.AddChild(CreateCommandEntry(cmd));
        }

        if (native.Count == 0 && devmode.Count == 0) {
            var noResult = new Label {
                Text = I18N.T("console.noResults", "No commands found."),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            noResult.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            listBox.AddChild(noResult);
        }
    }

    private static Control CreateCommandEntry(ConsoleBridge.CommandInfo cmd) {
        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 1);

        var nameBtn = new Button {
            Text = string.IsNullOrWhiteSpace(cmd.Args) ? cmd.Name : $"{cmd.Name}  {cmd.Args}",
            Alignment = HorizontalAlignment.Left,
            Flat = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = false,
            FocusMode = Control.FocusModeEnum.None
        };
        nameBtn.AddThemeColorOverride("font_color", new Color(0.45f, 0.85f, 0.55f));
        nameBtn.AddThemeColorOverride("font_hover_color", new Color(0.60f, 1.00f, 0.70f));
        nameBtn.AddThemeFontSizeOverride("font_size", 13);
        nameBtn.Pressed += () => DisplayServer.ClipboardSet(cmd.Name);
        container.AddChild(nameBtn);

        if (!string.IsNullOrWhiteSpace(cmd.Description)) {
            var descLabel = new Label {
                Text = $"  {cmd.Description}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            descLabel.AddThemeFontSizeOverride("font_size", 11);
            descLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            container.AddChild(descLabel);
        }

        var sep = new HSeparator();
        sep.Modulate = KitLibTheme.Separator;
        container.AddChild(sep);

        return container;
    }
}
