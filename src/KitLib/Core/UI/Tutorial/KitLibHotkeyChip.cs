using Godot;
using KitLib.Settings;

namespace KitLib.UI;

/// <summary>Keycap-style pill showing the current binding of one hotkey action.</summary>
internal static class KitLibHotkeyChip {
    private const int FontSize = 11;
    private const int CornerRadius = 6;

    /// <summary>Keycap for <paramref name="actionId"/>, reflecting the player's current rebinding.</summary>
    internal static Control Create(string actionId) {
        var panel = new PanelContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        panel.AddThemeStyleboxOverride("panel", CapStyle());

        var label = new Label {
            Text = SettingsStore.GetHotkeyBinding(actionId).FormatLabel(),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", FontSize);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        KitLibLocaleFonts.ApplyControl(label);
        panel.AddChild(label);
        return panel;
    }

    /// <summary>Row pairing a caption with the action's keycap, for settings lists.</summary>
    internal static Control CreateRow(string description, string actionId) {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);

        var label = new Label {
            Text = description,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", FontSize);
        label.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        KitLibLocaleFonts.ApplyControl(label);

        row.AddChild(label);
        row.AddChild(Create(actionId));
        return row;
    }

    private static StyleBoxFlat CapStyle() {
        var border = KitLibTheme.PanelBorder;
        return new StyleBoxFlat {
            BgColor = KitLibTheme.ButtonBgNormal,
            BorderColor = new Color(border.R, border.G, border.B, Mathf.Clamp(border.A * 1.8f, 0.12f, 0.34f)),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = CornerRadius,
            CornerRadiusTopRight = CornerRadius,
            CornerRadiusBottomRight = CornerRadius,
            CornerRadiusBottomLeft = CornerRadius,
            ContentMarginLeft = 7,
            ContentMarginRight = 7,
            ContentMarginTop = 1,
            ContentMarginBottom = 1,
            ShadowSize = 0,
        };
    }
}
