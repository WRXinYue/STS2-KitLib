using System;
using KitLib.Feedback;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class CrashRecoveryPromptUI {
    private const string PromptName = "KitLibCrashRecoveryPrompt";

    private static bool DismissedForSession { get; set; }

    public static bool IsVisible =>
        GodotObject.IsInstanceValid(
            Engine.GetMainLoop() is SceneTree tree
                ? tree.Root?.FindChild(PromptName, true, false)
                : null);

    public static bool TryShowStartupPrompt(NMainMenu mainMenu) {
        if (DismissedForSession)
            return false;

        if (!SettingsStore.Current.PromptOnCrashFeedback)
            return false;

        var report = CrashRecoveryStore.TryConsumePendingReport();
        if (report == null)
            return false;

        MainFile.Logger.Info("[KitLib CrashRecovery] Showing startup crash recovery prompt.");
        Callable.From(() => Show(mainMenu, report)).CallDeferred();
        return true;
    }

    public static void HideAnywhere() {
        var tree = Engine.GetMainLoop() as SceneTree;
        var root = tree?.Root;
        if (root == null)
            return;

        foreach (var node in root.FindChildren(PromptName, recursive: true, owned: false))
            node.QueueFree();
    }

    private static void Show(NMainMenu mainMenu, CrashReport report) {
        if (DismissedForSession || !GodotObject.IsInstanceValid(mainMenu))
            return;

        var root = mainMenu.GetTree().Root;
        HideAnywhere();

        var prefill = new FeedbackPrefill(
            CrashRecoveryStore.FormatPrefillTitle(report),
            CrashRecoveryStore.FormatPrefillDescription(report));

        var titleText = report.Kind == CrashReportKind.OrphanSession
            ? I18N.T("crashRecovery.startup.title", "Previous session may have crashed")
            : I18N.T("errorFeedback.prompt.title", "Something went wrong");

        var overlay = BuildOverlay(
            titleText,
            CrashRecoveryStore.FormatPromptBody(report),
            onExport: () => {
                Acknowledge();
                HideAnywhere();
                FeedbackReportUI.ShowOnMainMenu(mainMenu, prefill);
            },
            onDismiss: Acknowledge);

        root.AddChild(overlay);
    }

    private static void Acknowledge() {
        DismissedForSession = true;
        CrashRecoveryStore.ClearPendingReport();
        HideAnywhere();
    }

    private static Control BuildOverlay(
        string titleText,
        string bodyText,
        Action onExport,
        Action onDismiss) {
        var overlay = new Control {
            Name = PromptName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 2050,
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new ColorRect {
            Color = new Color(0, 0, 0, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += e => {
            if (e is InputEventMouseButton { Pressed: true })
                Callable.From(onDismiss).CallDeferred();
        };
        overlay.AddChild(backdrop);

        var wrapper = new CenterContainer();
        wrapper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(wrapper);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(480, 0) };
        panel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
        wrapper.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);

        var title = new Label {
            Text = titleText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        vbox.AddChild(title);

        vbox.AddChild(new ColorRect {
            Color = KitLibTheme.Separator,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });

        var body = new Label {
            Text = bodyText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        body.AddThemeFontSizeOverride("font_size", 12);
        body.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        vbox.AddChild(body);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 10);

        var dismissBtn = new Button {
            Text = I18N.T("crashRecovery.startup.notNow", "Not now"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        dismissBtn.Pressed += () => Callable.From(onDismiss).CallDeferred();
        btnRow.AddChild(dismissBtn);

        var exportBtn = new Button {
            Text = I18N.T("errorFeedback.prompt.exportReport", "Export feedback ZIP"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        exportBtn.Pressed += () => Callable.From(onExport).CallDeferred();
        btnRow.AddChild(exportBtn);

        vbox.AddChild(btnRow);
        panel.AddChild(vbox);

        exportBtn.GrabFocus();
        return overlay;
    }

    private static StyleBoxFlat CreateOverlayPanelStyle() => new() {
        BgColor = new Color(0.12f, 0.12f, 0.15f, 0.98f),
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
        ContentMarginLeft = 24,
        ContentMarginRight = 24,
        ContentMarginTop = 20,
        ContentMarginBottom = 20,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderColor = new Color(0.35f, 0.35f, 0.45f, 0.7f),
    };
}
