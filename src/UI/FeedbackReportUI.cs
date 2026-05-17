using System;
using System.Threading.Tasks;
using DevMode.Feedback;
using DevMode.Icons;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace DevMode.UI;

/// <summary>
/// Mod feedback / bug report panel — lets the user enter a title + description,
/// then exports a ZIP containing filtered logs, Harmony dump, framework bridge
/// snapshot, and loaded mod list.
/// </summary>
internal static class FeedbackReportUI {
    private const string RootName = "DevModeFeedbackReport";
    private const float PanelW = 640f;

    public static void Show(NGlobalUi globalUi) {
        Remove(globalUi);

        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, PanelW, () => Remove(globalUi), contentSeparation: 12);

        // ── Title ──────────────────────────────────────────────────────────
        var titleBox = new VBoxContainer();
        titleBox.AddThemeConstantOverride("separation", 4);
        titleBox.AddChild(DevPanelUI.CreatePanelTitle(I18N.T("feedback.title", "Mod Feedback / Bug Report")));
        var subtitle = new Label {
            Text = I18N.T("feedback.subtitle",
                "Fill in a title and description, then export a ZIP package to share with any mod author. Includes filtered logs, loaded mod list, Harmony patches, and framework info."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeFontSizeOverride("font_size", 11);
        subtitle.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
        titleBox.AddChild(subtitle);
        vbox.AddChild(titleBox);
        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());

        // ── Form ───────────────────────────────────────────────────────────
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 10);

        // Title field
        var titleLabel = MakeFieldLabel(I18N.T("feedback.field.title", "Title (one-line summary)"));
        var titleInput = new LineEdit {
            PlaceholderText = I18N.T("feedback.field.title.placeholder", ""),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28)
        };
        titleInput.AddThemeFontSizeOverride("font_size", 12);
        form.AddChild(titleLabel);
        form.AddChild(titleInput);

        // Description field
        var descLabel = MakeFieldLabel(I18N.T("feedback.field.desc", "Description / Steps to reproduce"));
        var descInput = new TextEdit {
            PlaceholderText = I18N.T("feedback.field.desc.placeholder", ""),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 140),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        descInput.AddThemeFontSizeOverride("font_size", 11);
        descInput.AddThemeStyleboxOverride("normal", MakeInputStyle());
        descInput.AddThemeStyleboxOverride("focus", MakeInputFocusStyle());
        form.AddChild(descLabel);
        form.AddChild(descInput);

        vbox.AddChild(form);

        // ── Contents hint ──────────────────────────────────────────────────
        var contentsCard = MakeContentsCard();
        vbox.AddChild(contentsCard);

        // ── Status label ───────────────────────────────────────────────────
        var statusLabel = new Label {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false
        };
        statusLabel.AddThemeFontSizeOverride("font_size", 11);
        statusLabel.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
        vbox.AddChild(statusLabel);

        // ── Export button ──────────────────────────────────────────────────
        var exportBtn = new Button {
            Text = I18N.T("feedback.export", "Export report ZIP"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 36),
            Icon = MdiIcon.ZipBox.Texture(16, Colors.White),
            Alignment = HorizontalAlignment.Center,
            FocusMode = Control.FocusModeEnum.None
        };
        exportBtn.AddThemeFontSizeOverride("font_size", 13);
        var accent = DevModeTheme.Accent;
        StyleBoxFlat MakeExportStyle(Color bg) => new() {
            BgColor = bg,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 14, ContentMarginRight = 14,
            ContentMarginTop = 4, ContentMarginBottom = 4,
        };
        exportBtn.AddThemeStyleboxOverride("normal", MakeExportStyle(new Color(accent.R, accent.G, accent.B, 0.45f)));
        exportBtn.AddThemeStyleboxOverride("hover", MakeExportStyle(new Color(accent.R, accent.G, accent.B, 0.65f)));
        exportBtn.AddThemeStyleboxOverride("pressed", MakeExportStyle(new Color(accent.R, accent.G, accent.B, 0.30f)));
        exportBtn.AddThemeStyleboxOverride("focus", MakeExportStyle(new Color(accent.R, accent.G, accent.B, 0.45f)));
        exportBtn.AddThemeStyleboxOverride("disabled", MakeExportStyle(DevModeTheme.ButtonBgNormal));
        exportBtn.AddThemeColorOverride("font_color", Colors.White);
        exportBtn.AddThemeColorOverride("font_hover_color", Colors.White);
        exportBtn.AddThemeColorOverride("font_pressed_color", Colors.White);
        exportBtn.AddThemeColorOverride("font_disabled_color", DevModeTheme.Subtle);
        vbox.AddChild(exportBtn);

        exportBtn.Pressed += () => {
            exportBtn.Disabled = true;
            exportBtn.Text = I18N.T("feedback.exporting", "Generating…");
            statusLabel.Visible = false;

            var req = new FeedbackReportBuilder.BuildRequest(
                Title: titleInput.Text,
                Description: descInput.Text);

            TaskHelper.RunSafely(RunExport(req, exportBtn, statusLabel, root));
        };

        ((Node)globalUi).AddChild(root);
    }

    public static void Remove(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
    }

    // ── Export logic ──────────────────────────────────────────────────────

    private static async Task RunExport(
        FeedbackReportBuilder.BuildRequest req,
        Button btn,
        Label statusLabel,
        Node root) {
        string? zipPath = null;
        string? errorMsg = null;

        await Task.Run(() => {
            try {
                zipPath = FeedbackReportBuilder.Build(req);
            }
            catch (Exception ex) {
                errorMsg = ex.Message;
            }
        });

        if (!GodotObject.IsInstanceValid(btn)) return;

        btn.Disabled = false;
        btn.Text = I18N.T("feedback.export", "Export report ZIP");
        statusLabel.Visible = true;

        if (zipPath != null) {
            statusLabel.Text = I18N.T("feedback.success", "Saved: {0}", zipPath);
            statusLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 0.60f));

            // Open containing folder in OS file manager
            var dir = System.IO.Path.GetDirectoryName(zipPath) ?? zipPath;
            OS.ShellShowInFileManager(dir);
        }
        else {
            statusLabel.Text = I18N.T("feedback.error", "Export failed: {0}", errorMsg ?? "unknown error");
            statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.42f, 0.42f));
            MainFile.Logger.Warn($"[DevMode Feedback] Export failed: {errorMsg}");
        }
    }

    // ── Widget helpers ────────────────────────────────────────────────────

    private static Label MakeFieldLabel(string text) {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", 11);
        l.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
        return l;
    }

    private static Control MakeContentsCard() {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var style = new StyleBoxFlat {
            BgColor = new Color(DevModeTheme.PanelBg.R, DevModeTheme.PanelBg.G, DevModeTheme.PanelBg.B, 0.45f),
            BorderColor = DevModeTheme.PanelBorder,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 8, ContentMarginBottom = 8
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var inner = new VBoxContainer();
        inner.AddThemeConstantOverride("separation", 3);

        var head = new Label { Text = I18N.T("feedback.contents.title", "ZIP contents") };
        head.AddThemeFontSizeOverride("font_size", 10);
        head.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
        inner.AddChild(head);

        string[] items = [
            "report.txt — " + I18N.T("feedback.contents.report", "Title, description, OS info, DevMode version"),
            "mods.txt — " + I18N.T("feedback.contents.mods", "Loaded mod list (id / name / version)"),
            "logs-filtered.txt — " + I18N.T("feedback.contents.logs", "In-memory log snapshot with noise suppressed"),
            "harmony-patches.txt — " + I18N.T("feedback.contents.harmony", "Full Harmony patch dump"),
            "framework-bridge.txt — " + I18N.T("feedback.contents.bridge", "RitsuLib & Harmony summary snapshot"),
        ];

        foreach (var item in items) {
            var l = new Label { Text = "  • " + item };
            l.AddThemeFontSizeOverride("font_size", 10);
            l.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            inner.AddChild(l);
        }

        panel.AddChild(inner);
        return panel;
    }

    private static StyleBoxFlat MakeInputStyle() =>
        new() {
            BgColor = new Color(0f, 0f, 0f, 0.22f),
            BorderColor = DevModeTheme.PanelBorder,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8, ContentMarginRight = 8,
            ContentMarginTop = 6, ContentMarginBottom = 6
        };

    private static StyleBoxFlat MakeInputFocusStyle() {
        var s = MakeInputStyle();
        s.BorderColor = DevModeTheme.Accent;
        return s;
    }
}
