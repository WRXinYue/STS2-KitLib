using Godot;

namespace KitLib.UI;

internal static class QuickSlToastUI {
    private const string ToastName = "KitLibQuickSlToast";

    internal static void Show(string message) {
        var tree = Engine.GetMainLoop() as SceneTree;
        var root = tree?.Root;
        if (root == null)
            return;

        foreach (var node in root.FindChildren(ToastName, recursive: true, owned: false))
            node.QueueFree();

        var panel = new PanelContainer { Name = ToastName };
        panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        panel.OffsetTop = 48;
        panel.OffsetBottom = 48;
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;

        var style = new StyleBoxFlat {
            BgColor = KitLibTheme.PanelBg with { A = 0.92f },
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var label = new Label {
            Text = message,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        panel.AddChild(label);

        root.AddChild(panel);

        var tween = panel.CreateTween();
        tween.TweenInterval(1.0);
        tween.TweenProperty(panel, "modulate:a", 0f, 0.5)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() => {
            if (GodotObject.IsInstanceValid(panel))
                panel.QueueFree();
        }));
    }
}
