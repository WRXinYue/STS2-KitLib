using System;
using KitLib.Feedback;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class ErrorFeedbackPromptUI {
    private const string PromptName = "KitLibErrorFeedbackPrompt";

    private static bool ShownForSession { get; set; }

    public static bool IsVisible =>
        GodotObject.IsInstanceValid(
            Engine.GetMainLoop() is SceneTree tree
                ? tree.Root?.FindChild(PromptName, true, false)
                : null);

    public static bool TryShowFromCrash(CrashReport report) {
        if (ShownForSession)
            return false;

        if (!SettingsStore.Current.PromptOnCrashFeedback)
            return false;

        if (IsVisible)
            return false;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return false;

        ShownForSession = true;
        Callable.From(() => Show(tree.Root, report)).CallDeferred();
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

    private static void Acknowledge() {
        ShownForSession = true;
        CrashRecoveryStore.ClearPendingReport();
        HideAnywhere();
    }

    private static void Show(Node attachRoot, CrashReport report) {
        if (!GodotObject.IsInstanceValid(attachRoot))
            return;

        HideAnywhere();

        var prefill = new FeedbackPrefill(
            CrashRecoveryStore.FormatPrefillTitle(report),
            CrashRecoveryStore.FormatPrefillDescription(report));

        var overlay = BuildOverlay(
            CrashRecoveryStore.FormatPromptBody(report),
            onViewLogs: () => OpenLogs(attachRoot),
            onExport: () => {
                Acknowledge();
                OpenFeedback(attachRoot, prefill);
            },
            onDismiss: Acknowledge);

        attachRoot.AddChild(overlay);
    }

    private static void OpenLogs(Node attachRoot) {
        HideAnywhere();
        Callable.From(() => {
            if (TryFindGlobalUi(attachRoot, out var globalUi)) {
                LogViewerUI.Show(globalUi);
                return;
            }

            if (TryFindMainMenu(attachRoot, out var mainMenu))
                LogViewerUI.ShowOnMainMenu(mainMenu);
        }).CallDeferred();
    }

    private static void OpenFeedback(Node attachRoot, FeedbackPrefill prefill) {
        Callable.From(() => {
            if (TryFindGlobalUi(attachRoot, out var globalUi)) {
                FeedbackReportUI.Show(globalUi, prefill);
                return;
            }

            if (TryFindMainMenu(attachRoot, out var mainMenu))
                FeedbackReportUI.ShowOnMainMenu(mainMenu, prefill);
        }).CallDeferred();
    }

    private static bool TryFindGlobalUi(Node root, out NGlobalUi globalUi) {
        foreach (var node in root.FindChildren("*", recursive: true, owned: false)) {
            if (node is NGlobalUi ui) {
                globalUi = ui;
                return true;
            }
        }

        globalUi = null!;
        return false;
    }

    private static bool TryFindMainMenu(Node root, out NMainMenu mainMenu) {
        foreach (var node in root.FindChildren("*", recursive: true, owned: false)) {
            if (node is NMainMenu menu) {
                mainMenu = menu;
                return true;
            }
        }

        mainMenu = null!;
        return false;
    }

    private static Control BuildOverlay(
        string bodyText,
        Action onViewLogs,
        Action onExport,
        Action onDismiss) {
        var overlay = new Control {
            Name = PromptName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 2060,
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new ColorRect {
            Color = new Color(0, 0, 0, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
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
            Text = I18N.T("errorFeedback.prompt.title", "Something went wrong"),
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
            Text = I18N.T("errorFeedback.prompt.dismiss", "Close"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        dismissBtn.Pressed += () => Callable.From(onDismiss).CallDeferred();
        btnRow.AddChild(dismissBtn);

        var logsBtn = new Button {
            Text = I18N.T("errorFeedback.prompt.viewLogs", "View logs"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        logsBtn.Pressed += () => Callable.From(onViewLogs).CallDeferred();
        btnRow.AddChild(logsBtn);

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
