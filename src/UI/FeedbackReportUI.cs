using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KitLib.Feedback;
using KitLib.Icons;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

/// <summary>
/// Mod feedback / bug report panel — lets the user enter a title + description,
/// select a game log file to attach, toggle privacy mode, and export a ZIP.
/// </summary>
internal static class FeedbackReportUI {
    private const string RootName = "KitLibFeedbackReport";
    private const float PanelW = 660f;

    // Sentinel index in the OptionButton meaning "do not attach a log file"
    private const int NoLogIndex = 0;

    public static void Show(NGlobalUi globalUi, FeedbackPrefill? prefill = null) {
        var parent = (Node)globalUi;
        Remove(parent);
        Action close = () => Remove(parent);
        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, PanelW, close, contentSeparation: 12);
        BuildPanel(vbox, prefill);
        parent.AddChild(root);
    }

    public static void ShowOnMainMenu(NMainMenu mainMenu, FeedbackPrefill? prefill = null) {
        var parent = mainMenu.GetTree().Root;
        HideAnywhere();
        Action close = HideAnywhere;
        var (_, vbox) = DevMainMenuOverlay.Create(parent, RootName, PanelW, close, contentSeparation: 12);
        BuildPanel(vbox, prefill);
    }

    private static void BuildPanel(VBoxContainer vbox, FeedbackPrefill? prefill = null) {
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
        subtitle.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        titleBox.AddChild(subtitle);
        vbox.AddChild(titleBox);
        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());

        // ── Form ───────────────────────────────────────────────────────────
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 10);

        // Title field
        form.AddChild(MakeFieldLabel(I18N.T("feedback.field.title", "Title (one-line summary)")));
        var titleInput = new LineEdit {
            PlaceholderText = I18N.T("feedback.field.title.placeholder", ""),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28)
        };
        titleInput.AddThemeFontSizeOverride("font_size", 12);
        if (prefill != null && !string.IsNullOrWhiteSpace(prefill.Value.Title))
            titleInput.Text = prefill.Value.Title;
        form.AddChild(titleInput);

        // Description field
        form.AddChild(MakeFieldLabel(I18N.T("feedback.field.desc", "Description / Steps to reproduce")));
        var descInput = new TextEdit {
            PlaceholderText = I18N.T("feedback.field.desc.placeholder", ""),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 120),
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        descInput.AddThemeFontSizeOverride("font_size", 11);
        descInput.AddThemeStyleboxOverride("normal", MakeInputStyle());
        descInput.AddThemeStyleboxOverride("focus", MakeInputFocusStyle());
        if (prefill != null && !string.IsNullOrWhiteSpace(prefill.Value.Description))
            descInput.Text = prefill.Value.Description;
        form.AddChild(descInput);

        vbox.AddChild(form);

        // ── Log file selection ─────────────────────────────────────────────
        var logFiles = FeedbackReportBuilder.ScanLogFiles();
        var logOption = BuildLogDropdown(logFiles, out int defaultLogIndex);
        var logRow = new HBoxContainer();
        logRow.AddThemeConstantOverride("separation", 8);
        var logLabel = MakeFieldLabel(I18N.T("feedback.log.label", "Attach game log file"));
        logLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        logLabel.VerticalAlignment = VerticalAlignment.Center;
        logRow.AddChild(logLabel);
        logRow.AddChild(logOption);
        vbox.AddChild(logRow);

        // ── Privacy mode ───────────────────────────────────────────────────
        var privacyRow = new HBoxContainer();
        privacyRow.AddThemeConstantOverride("separation", 8);
        var privacyToggle = new CheckButton {
            Text = I18N.T("feedback.privacy.label", "Privacy mode"),
            ButtonPressed = true,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            FocusMode = Control.FocusModeEnum.None
        };
        privacyToggle.AddThemeFontSizeOverride("font_size", 11);
        var privacyHint = new Label {
            Text = I18N.T("feedback.privacy.hint", "Replaces your file system path with <user-data>"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        privacyHint.AddThemeFontSizeOverride("font_size", 10);
        privacyHint.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        privacyRow.AddChild(privacyToggle);
        privacyRow.AddChild(privacyHint);
        vbox.AddChild(privacyRow);

        // ── Contents hint ──────────────────────────────────────────────────
        var contentsCard = MakeContentsCard(logFiles.Count > 0);
        vbox.AddChild(contentsCard);

        // ── Status label ───────────────────────────────────────────────────
        var statusLabel = new Label {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false
        };
        statusLabel.AddThemeFontSizeOverride("font_size", 11);
        statusLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        vbox.AddChild(statusLabel);

        // ── Export button ──────────────────────────────────────────────────
        var exportBtn = BuildExportButton();
        vbox.AddChild(exportBtn);

        exportBtn.Pressed += () => {
            exportBtn.Disabled = true;
            exportBtn.Text = I18N.T("feedback.exporting", "Generating…");
            statusLabel.Visible = false;

            // Resolve selected log file path
            string? selectedLog = null;
            int sel = logOption.Selected;
            if (sel != NoLogIndex && sel - 1 < logFiles.Count)
                selectedLog = logFiles[sel - 1].AbsPath;

            var req = new FeedbackReportBuilder.BuildRequest(
                Title: titleInput.Text,
                Description: descInput.Text,
                LogFilePath: selectedLog,
                PrivacyMode: privacyToggle.ButtonPressed);

            TaskHelper.RunSafely(RunExport(req, exportBtn, statusLabel));
        };
    }

    public static void Remove(NGlobalUi globalUi) => Remove((Node)globalUi);

    public static void Remove(Node parent) => HideAnywhere();

    public static void HideAnywhere() => DevMainMenuOverlay.RemoveAnywhere(RootName);

    // ── Log file dropdown ─────────────────────────────────────────────────

    private static OptionButton BuildLogDropdown(
        IReadOnlyList<(string DisplayName, string AbsPath)> logFiles,
        out int defaultIndex) {

        var opt = new OptionButton {
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            CustomMinimumSize = new Vector2(220, 26),
            FocusMode = Control.FocusModeEnum.None
        };
        opt.AddThemeFontSizeOverride("font_size", 11);

        // Index 0 = no log file
        opt.AddItem(I18N.T("feedback.log.none", "(none)"), 0);

        foreach (var (name, _) in logFiles)
            opt.AddItem(name);

        // Default: select the first (most recent) log file if any
        if (logFiles.Count > 0) {
            opt.Selected = 1;
            defaultIndex = 1;
        }
        else {
            opt.Selected = 0;
            defaultIndex = 0;
        }

        return opt;
    }

    // ── Export logic ──────────────────────────────────────────────────────

    private static async Task RunExport(
        FeedbackReportBuilder.BuildRequest req,
        Button btn,
        Label statusLabel) {
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
            OS.ShellShowInFileManager(Path.GetDirectoryName(zipPath) ?? zipPath);
        }
        else {
            statusLabel.Text = I18N.T("feedback.error", "Export failed: {0}", errorMsg ?? "unknown error");
            statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.42f, 0.42f));
            MainFile.Logger.Warn($"[KitLib Feedback] Export failed: {errorMsg}");
        }
    }

    // ── Widget helpers ────────────────────────────────────────────────────

    private static Label MakeFieldLabel(string text) {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", 11);
        l.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        return l;
    }

    private static Button BuildExportButton() {
        var btn = new Button {
            Text = I18N.T("feedback.export", "Export report ZIP"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 36),
            Icon = MdiIcon.ZipBox.Texture(16, Colors.White),
            Alignment = HorizontalAlignment.Center,
            FocusMode = Control.FocusModeEnum.None
        };
        btn.AddThemeFontSizeOverride("font_size", 13);
        var accent = KitLibTheme.Accent;
        StyleBoxFlat MakeStyle(Color bg) => new() {
            BgColor = bg,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 14, ContentMarginRight = 14,
            ContentMarginTop = 4, ContentMarginBottom = 4,
        };
        btn.AddThemeStyleboxOverride("normal",   MakeStyle(new Color(accent.R, accent.G, accent.B, 0.45f)));
        btn.AddThemeStyleboxOverride("hover",    MakeStyle(new Color(accent.R, accent.G, accent.B, 0.65f)));
        btn.AddThemeStyleboxOverride("pressed",  MakeStyle(new Color(accent.R, accent.G, accent.B, 0.30f)));
        btn.AddThemeStyleboxOverride("focus",    MakeStyle(new Color(accent.R, accent.G, accent.B, 0.45f)));
        btn.AddThemeStyleboxOverride("disabled", MakeStyle(KitLibTheme.ButtonBgNormal));
        btn.AddThemeColorOverride("font_color",          Colors.White);
        btn.AddThemeColorOverride("font_hover_color",    Colors.White);
        btn.AddThemeColorOverride("font_pressed_color",  Colors.White);
        btn.AddThemeColorOverride("font_disabled_color", KitLibTheme.Subtle);
        return btn;
    }

    private static Control MakeContentsCard(bool hasLogFiles) {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var style = new StyleBoxFlat {
            BgColor = new Color(KitLibTheme.PanelBg.R, KitLibTheme.PanelBg.G, KitLibTheme.PanelBg.B, 0.45f),
            BorderColor = KitLibTheme.PanelBorder,
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
        head.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        inner.AddChild(head);

        var items = new List<string> {
            "report.txt — " + I18N.T("feedback.contents.report", "Title, description, OS info, DevMode version"),
            "mods.txt — " + I18N.T("feedback.contents.mods", "Loaded mod list (id / name / version)"),
            "logs-filtered.txt — " + I18N.T("feedback.contents.logs", "In-memory log snapshot with noise suppressed"),
            "harmony-patches.txt — " + I18N.T("feedback.contents.harmony", "Full Harmony patch dump"),
            "framework-bridge.txt — " + I18N.T("feedback.contents.bridge", "RitsuLib & Harmony summary snapshot"),
        };

        if (hasLogFiles)
            items.Add("game-logs/<file> — " + I18N.T("feedback.contents.gamelog", "Selected game log file (last 512 KB)"));

        foreach (var item in items) {
            var l = new Label { Text = "  • " + item };
            l.AddThemeFontSizeOverride("font_size", 10);
            l.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            inner.AddChild(l);
        }

        panel.AddChild(inner);
        return panel;
    }

    private static StyleBoxFlat MakeInputStyle() =>
        new() {
            BgColor = new Color(0f, 0f, 0f, 0.22f),
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8, ContentMarginRight = 8,
            ContentMarginTop = 6, ContentMarginBottom = 6
        };

    private static StyleBoxFlat MakeInputFocusStyle() {
        var s = MakeInputStyle();
        s.BorderColor = KitLibTheme.Accent;
        return s;
    }
}
