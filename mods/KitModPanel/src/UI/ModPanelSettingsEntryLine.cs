using System;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace KitLib.UI;

internal static class ModPanelSettingsEntryLine {
    internal const string LineNodeName = "KitLibModPanel";
    internal const string DividerNodeName = "KitLibModPanelDivider";
    internal const string ButtonNodeName = "KitLibModPanelButton";

    internal static MarginContainer Create(Action openAction) {
        var line = new MarginContainer {
            Name = LineNodeName,
            CustomMinimumSize = new Vector2(0f, 64f),
        };
        line.AddThemeConstantOverride("margin_left", 12);
        line.AddThemeConstantOverride("margin_right", 12);

        var row = new HBoxContainer {
            Name = "ContentRow",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        line.AddChild(row);

        var label = CreateRowLabel();
        row.AddChild(label);

        var button = new ModPanelSettingsEntryButton(
            I18N.T("modpanel.settings.entry.button", "Open"),
            openAction) {
            Name = ButtonNodeName,
        };
        row.AddChild(button);

        return line;
    }

    internal static void RefreshButton(MarginContainer line) {
        line.Visible = true;

        if (line.GetNodeOrNull<MegaRichTextLabel>("ContentRow/Label") is { } label)
            label.SetTextAutoSize(I18N.T("modpanel.settings.entry.title", "Mods (KitLib)"));

        if (line.GetNodeOrNull<ModPanelSettingsEntryButton>($"ContentRow/{ButtonNodeName}") is { } button)
            button.Enable();
    }

    static MegaRichTextLabel CreateRowLabel() {
        const int fontSize = 28;
        var label = new MegaRichTextLabel {
            Name = "Label",
            BbcodeEnabled = true,
            AutoSizeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            ClipContents = false,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Theme = ModPanelSettingsEntryResources.SettingsLineTheme,
            IsHorizontallyBound = true,
            Modulate = Colors.White,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("normal_font_size", fontSize);
        label.MinFontSize = 18;
        label.MaxFontSize = fontSize;
        label.SetTextAutoSize(I18N.T("modpanel.settings.entry.title", "Mods (KitLib)"));
        ModPanelUI.ApplyMegaRichTextFontOverrides(label);
        return label;
    }

    internal static ColorRect CreateDivider() =>
        new() {
            Name = DividerNodeName,
            Color = new Color(1f, 1f, 1f, 0.12f),
            CustomMinimumSize = new Vector2(0f, 1f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
}
