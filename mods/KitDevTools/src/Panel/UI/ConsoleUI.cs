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

        var dual = DevPanelUI.CreateMainOnlyDualOverlay(
            globalUi, RootName, PanelW, () => Remove(globalUi));
        var vbox = dual.MainContent;

        BuildNavTab(vbox);

        var (searchRow, searchInput) = DevPanelUI.CreateSearchRow(I18N.T("console.search", "Filter commands..."));
        vbox.AddChild(searchRow);

        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var listBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        listBox.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(listBox);
        vbox.AddChild(scroll);

        var hint = new Label {
            Text = I18N.T("console.copyHint", "Click a command to copy to clipboard"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color(0.38f, 0.38f, 0.45f));
        vbox.AddChild(hint);

        PopulateCommands(listBox, "");
        searchInput.TextChanged += filter => PopulateCommands(listBox, filter);

        dual.AttachToScene();
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
                c.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                c.SourceLabel.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                ConsoleBridge.LocalizeDescription(c).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        var official = filtered
            .Where(c => c.SourceKind == ConsoleBridge.CommandSourceKind.Official)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var kitlib = filtered
            .Where(c => c.SourceKind == ConsoleBridge.CommandSourceKind.KitLib)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var modGroups = filtered
            .Where(c => c.SourceKind == ConsoleBridge.CommandSourceKind.Mod)
            .GroupBy(c => c.SourceLabel, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList())
            .ToList();

        int sectionCount = 0;
        sectionCount += AddSection(
            listBox,
            official,
            $"{I18N.T("console.section.official", "Official Commands")}  ({official.Count})");
        sectionCount += AddSection(
            listBox,
            kitlib,
            $"{I18N.T("console.section.kitlib", "KitLib Commands")}  ({kitlib.Count})");

        foreach (var group in modGroups) {
            string header = I18N.T("console.section.mod", "{0} Commands", group[0].SourceLabel);
            sectionCount += AddSection(listBox, group, $"{header}  ({group.Count})");
        }

        if (sectionCount == 0) {
            var noResult = new Label {
                Text = I18N.T("console.noResults", "No commands found."),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            noResult.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            listBox.AddChild(noResult);
        }
    }

    private static int AddSection(
        VBoxContainer listBox,
        IReadOnlyList<ConsoleBridge.CommandInfo> commands,
        string header) {
        if (commands.Count == 0) return 0;

        listBox.AddChild(DevPanelUI.CreateSectionHeader(header));
        foreach (var cmd in commands)
            listBox.AddChild(CreateCommandEntry(cmd));
        return 1;
    }

    private static Control CreateCommandEntry(ConsoleBridge.CommandInfo cmd) {
        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 1);

        string commandText = string.IsNullOrWhiteSpace(cmd.Args)
            ? cmd.Name
            : $"{cmd.Name}  {cmd.Args}";
        string lineText = cmd.SourceKind == ConsoleBridge.CommandSourceKind.Mod
            ? $"[{ConsoleBridge.SourceBadge(cmd)}]  {commandText}"
            : commandText;

        var nameBtn = new Button {
            Text = lineText,
            Alignment = HorizontalAlignment.Left,
            Flat = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = false,
            FocusMode = Control.FocusModeEnum.None
        };
        nameBtn.AddThemeColorOverride("font_color", SourceNameColor(cmd.SourceKind));
        nameBtn.AddThemeColorOverride("font_hover_color", SourceNameHoverColor(cmd.SourceKind));
        nameBtn.AddThemeFontSizeOverride("font_size", 13);
        nameBtn.Pressed += () => DisplayServer.ClipboardSet(cmd.Name);
        container.AddChild(nameBtn);

        string description = ConsoleBridge.LocalizeDescription(cmd);
        if (!string.IsNullOrWhiteSpace(description)) {
            var descLabel = new Label {
                Text = $"  {description}",
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

    private static Color SourceNameColor(ConsoleBridge.CommandSourceKind kind) => kind switch {
        ConsoleBridge.CommandSourceKind.Official => new Color(0.55f, 0.75f, 0.95f),
        ConsoleBridge.CommandSourceKind.KitLib => new Color(0.45f, 0.85f, 0.55f),
        _ => new Color(0.82f, 0.72f, 0.45f),
    };

    private static Color SourceNameHoverColor(ConsoleBridge.CommandSourceKind kind) => kind switch {
        ConsoleBridge.CommandSourceKind.Official => new Color(0.70f, 0.88f, 1.00f),
        ConsoleBridge.CommandSourceKind.KitLib => new Color(0.60f, 1.00f, 0.70f),
        _ => new Color(0.95f, 0.85f, 0.55f),
    };
}
