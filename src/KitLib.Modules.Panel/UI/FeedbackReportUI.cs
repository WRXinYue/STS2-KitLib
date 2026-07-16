using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;
using KitLib;
using KitLib.Feedback;
using KitLib.Icons;
using MegaCrit.Sts2.Core.Helpers;

namespace KitLib.UI;

/// <summary>
/// Log export form — shown in the log viewer slide-out extension panel.
/// </summary>
internal static class FeedbackReportUI {
    internal static void BuildContent(VBoxContainer vbox, bool compact = false) {
        vbox.AddThemeConstantOverride("separation", compact ? 8 : 10);

        if (!compact) {
            var titleBox = new VBoxContainer();
            titleBox.AddThemeConstantOverride("separation", 4);
            titleBox.AddChild(DevPanelUI.CreatePanelTitle(I18N.T("log.export.title", "Log Export")));
            var subtitle = new Label {
                Text = I18N.T("log.export.subtitle",
                    "Export selected game log and diagnostics as a ZIP package."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            subtitle.AddThemeFontSizeOverride("font_size", 11);
            subtitle.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            titleBox.AddChild(subtitle);
            vbox.AddChild(titleBox);
            vbox.AddChild(DevPanelUI.CreateOverlaySeparator());
        }
        else {
            var heading = new Label { Text = I18N.T("log.export.title", "Log Export") };
            heading.AddThemeFontSizeOverride("font_size", 12);
            heading.AddThemeColorOverride("font_color", KitLibTheme.Accent);
            vbox.AddChild(heading);
        }

        var logFiles = FeedbackReportBuilder.ScanLogFiles();
        var logOption = BuildLogDropdown(logFiles, compact);
        var logRow = new VBoxContainer();
        logRow.AddThemeConstantOverride("separation", 4);
        logRow.AddChild(MakeFieldLabel(I18N.T("log.export.log.label", "Game log file")));
        logRow.AddChild(logOption);
        vbox.AddChild(logRow);

        if (logFiles.Count == 0) {
            var noLogHint = new Label {
                Text = I18N.T("log.export.log.missing", "No game log file found under user://logs/."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            noLogHint.AddThemeFontSizeOverride("font_size", 10);
            noLogHint.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.45f));
            vbox.AddChild(noLogHint);
        }

        var privacyToggle = new CheckButton {
            Text = I18N.T("log.export.privacy.label", "Privacy mode"),
            ButtonPressed = true,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            FocusMode = Control.FocusModeEnum.None
        };
        privacyToggle.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(privacyToggle);

        var privacyHint = new Label {
            Text = I18N.T("log.export.privacy.hint", "Replaces your file system path with <user-data>"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        privacyHint.AddThemeFontSizeOverride("font_size", 10);
        privacyHint.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        vbox.AddChild(privacyHint);

        vbox.AddChild(MakeContentsCard());

        var statusLabel = new Label {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false
        };
        statusLabel.AddThemeFontSizeOverride("font_size", 11);
        statusLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        vbox.AddChild(statusLabel);

        var exportBtn = BuildExportButton(compact);
        exportBtn.Disabled = logFiles.Count == 0;
        vbox.AddChild(exportBtn);

        exportBtn.Pressed += () => {
            if (logFiles.Count == 0 || logOption.Selected < 0 || logOption.Selected >= logFiles.Count)
                return;

            exportBtn.Disabled = true;
            exportBtn.Text = I18N.T("log.export.exporting", "Generating…");
            statusLabel.Visible = false;

            var req = new FeedbackReportBuilder.BuildRequest(
                LogFilePath: logFiles[logOption.Selected].AbsPath,
                PrivacyMode: privacyToggle.ButtonPressed);

            TaskHelper.RunSafely(RunExport(req, exportBtn, statusLabel, logFiles.Count == 0));
        };
    }

    private static OptionButton BuildLogDropdown(
        IReadOnlyList<(string DisplayName, string AbsPath, bool IsCurrentSession)> logFiles,
        bool compact) {

        var opt = new OptionButton {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 26),
            FocusMode = Control.FocusModeEnum.None,
            Disabled = logFiles.Count == 0
        };
        opt.AddThemeFontSizeOverride("font_size", 11);

        foreach (var (name, _, _) in logFiles)
            opt.AddItem(name);

        opt.Selected = ResolveDefaultLogIndex(logFiles);
        return opt;
    }

    static int ResolveDefaultLogIndex(
        IReadOnlyList<(string DisplayName, string AbsPath, bool IsCurrentSession)> logFiles) {
        if (logFiles.Count == 0)
            return -1;

        for (int i = 0; i < logFiles.Count; i++) {
            if (logFiles[i].IsCurrentSession)
                return i;
        }

        return 0;
    }

    internal static Task<(string? ZipPath, string? Error)> ExportDefaultZipAsync() {
        LogCollector.RefreshFileSnapshot();
        var logs = FeedbackReportBuilder.ScanLogFiles();
        if (logs.Count == 0)
            return Task.FromResult<(string?, string?)>((null, null));

        int idx = ResolveDefaultLogIndex(logs);
        var req = new FeedbackReportBuilder.BuildRequest(
            LogFilePath: logs[idx].AbsPath,
            PrivacyMode: true);

        return Task.Run(() => {
            try {
                return ((string?)FeedbackReportBuilder.Build(req), (string?)null);
            }
            catch (Exception ex) {
                return ((string?)null, ex.Message);
            }
        });
    }

    private static async Task RunExport(
        FeedbackReportBuilder.BuildRequest req,
        Button btn,
        Label statusLabel,
        bool noLogs) {
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

        btn.Disabled = noLogs;
        btn.Text = I18N.T("log.export.zip", "Export ZIP");
        statusLabel.Visible = true;

        if (zipPath != null) {
            statusLabel.Text = I18N.T("log.export.success", "Saved: {0}", zipPath);
            statusLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.95f, 0.60f));
            OS.ShellShowInFileManager(Path.GetDirectoryName(zipPath) ?? zipPath);
        }
        else {
            statusLabel.Text = I18N.T("log.export.error", "Export failed: {0}", errorMsg ?? "unknown error");
            statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.42f, 0.42f));
            KitLog.Warn("Feedback", $"Export failed: {errorMsg}");
        }
    }

    private static Label MakeFieldLabel(string text) {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", 11);
        l.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        return l;
    }

    private static Button BuildExportButton(bool compact) {
        var btn = new Button {
            Text = I18N.T("log.export.zip", "Export ZIP"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, compact ? 32 : 36),
            Icon = MdiIcon.ZipBox.Texture(16, Colors.White),
            Alignment = HorizontalAlignment.Center,
            FocusMode = Control.FocusModeEnum.None
        };
        btn.AddThemeFontSizeOverride("font_size", compact ? 12 : 13);
        var accent = KitLibTheme.Accent;
        StyleBoxFlat MakeStyle(Color bg) => new() {
            BgColor = bg,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
        btn.AddThemeStyleboxOverride("normal", MakeStyle(new Color(accent.R, accent.G, accent.B, 0.45f)));
        btn.AddThemeStyleboxOverride("hover", MakeStyle(new Color(accent.R, accent.G, accent.B, 0.65f)));
        btn.AddThemeStyleboxOverride("pressed", MakeStyle(new Color(accent.R, accent.G, accent.B, 0.30f)));
        btn.AddThemeStyleboxOverride("focus", MakeStyle(new Color(accent.R, accent.G, accent.B, 0.45f)));
        btn.AddThemeStyleboxOverride("disabled", MakeStyle(KitLibTheme.ButtonBgNormal));
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
        btn.AddThemeColorOverride("font_pressed_color", Colors.White);
        btn.AddThemeColorOverride("font_disabled_color", KitLibTheme.Subtle);
        return btn;
    }

    private static Control MakeContentsCard() {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var style = new StyleBoxFlat {
            BgColor = new Color(KitLibTheme.PanelBg.R, KitLibTheme.PanelBg.G, KitLibTheme.PanelBg.B, 0.45f),
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var inner = new VBoxContainer();
        inner.AddThemeConstantOverride("separation", 3);

        var head = new Label { Text = I18N.T("log.export.contents.title", "ZIP contents") };
        head.AddThemeFontSizeOverride("font_size", 10);
        head.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        inner.AddChild(head);

        foreach (var item in new[] {
            "harmony-patches.txt — " + I18N.T("log.export.contents.harmony", "Full Harmony patch dump"),
            "combat-stats.json — " + I18N.T("log.export.contents.combatStats", "Combat stats"),
            "godot.log — " + I18N.T("log.export.contents.gamelog", "Game log file"),
        }) {
            var l = new Label { Text = "  • " + item };
            l.AddThemeFontSizeOverride("font_size", 10);
            l.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            inner.AddChild(l);
        }

        panel.AddChild(inner);
        return panel;
    }
}
