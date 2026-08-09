using Godot;
using KitLib.Host;
using KitLib.ModPanel.Icons;
using KitLib.UI;

namespace KitLib.UI;

/// <summary>Per-mod Harmony patch detail overlay for ModPanel (replaces the old in-run analysis panel).</summary>
internal static class ModHarmonyDetailPopup {
    private const string RootName = "KitLibModHarmonyPopup";

    internal static void Show(Control host, string modId, string? installPath, string displayName) {
        if (!GodotObject.IsInstanceValid(host))
            return;

        host.GetNodeOrNull<Control>(RootName)?.QueueFree();

        var overlay = new Control {
            Name = RootName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 4000,
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.GrowHorizontal = Control.GrowDirection.Both;
        overlay.GrowVertical = Control.GrowDirection.Both;

        var dim = new ColorRect {
            Color = new Color(0f, 0f, 0f, 0.62f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(dim);

        var center = new CenterContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(center);

        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(720f, 520f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        var panelStyle = new StyleBoxFlat {
            BgColor = KitLibTheme.PanelBg,
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 14,
            ContentMarginBottom = 14,
        };
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        center.AddChild(panel);

        var vbox = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        vbox.AddThemeConstantOverride("separation", 10);
        panel.AddChild(vbox);

        var title = new Label {
            Text = string.Format(
                I18N.T("modpanel.harmony.popup.title", "Harmony patches — {0}"),
                displayName),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", ModPanelUiPalette.LabelPrimary);
        vbox.AddChild(title);

        var statsRow = new HBoxContainer();
        statsRow.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(statsRow);

        var report = KitLibPanelUiOps.BuildModHarmonyDetailReport?.Invoke(modId, installPath)
            ?? I18N.T("modpanel.harmony.unavailable", "Harmony analysis requires KitLib.Dev.");
        var stats = KitLibPanelUiOps.QueryModHarmonyPatchStats?.Invoke(modId, installPath);
        if (stats != null) {
            statsRow.AddChild(CreateHarmonyStatChip(string.Format("{0} patches", stats.Value.PatchOperations)));
            statsRow.AddChild(CreateHarmonyStatChip(string.Format("{0} methods", stats.Value.PatchedMethods)));
            if (stats.Value.HarmonyOwnerCount > 0)
                statsRow.AddChild(CreateHarmonyStatChip(string.Format("{0} owners", stats.Value.HarmonyOwnerCount)));
        }

        var body = new TextEdit {
            Text = report,
            Editable = false,
            WrapMode = TextEdit.LineWrappingMode.None,
            ContextMenuEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        body.AddThemeFontSizeOverride("font_size", 11);
        body.AddThemeColorOverride("font_color", ModPanelUiPalette.RichTextBody);
        vbox.AddChild(body);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 8);
        var copyBtn = new Button {
            Text = I18N.T("modpanel.harmony.popup.copy", "Copy report"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        copyBtn.Pressed += () => {
            if (!string.IsNullOrEmpty(body.Text))
                DisplayServer.ClipboardSet(body.Text);
        };
        btnRow.AddChild(copyBtn);
        var closeBtn = new Button {
            Text = I18N.T("modpanel.harmony.popup.close", "Close"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        closeBtn.Pressed += () => overlay.QueueFree();
        btnRow.AddChild(closeBtn);
        vbox.AddChild(btnRow);

        dim.GuiInput += ev => {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                overlay.QueueFree();
        };

        host.AddChild(overlay);
        closeBtn.CallDeferred(Control.MethodName.GrabFocus);
    }

    private static PanelContainer CreateHarmonyStatChip(string text) {
        var label = new Label {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            LabelSettings = new LabelSettings {
                FontSize = ModPanelUiMetrics.SidebarModListVersionBadgeFontSize,
                FontColor = ModPanelUiPalette.RichTextSecondary,
            },
        };
        var chip = new PanelContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TooltipText = text,
        };
        chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new Color(0.07f, 0.065f, 0.058f, 0.94f),
            BorderColor = new Color(
                ModPanelUiPalette.RichTextSecondary.R,
                ModPanelUiPalette.RichTextSecondary.G,
                ModPanelUiPalette.RichTextSecondary.B,
                0.5f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusBottomLeft = 4,
            ContentMarginLeft = 5,
            ContentMarginTop = 2,
            ContentMarginRight = 5,
            ContentMarginBottom = 2,
        });
        chip.AddChild(label);
        return chip;
    }
}
