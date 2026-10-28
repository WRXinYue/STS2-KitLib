using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;
using KitLib;
using KitLib.Feedback;
using KitLib.Icons;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

/// <summary>
/// Log export form — shown in the log viewer slide-out extension panel.
/// </summary>
internal static class FeedbackReportUI {
    static readonly (string Id, string Glyph, string Key, string Fallback)[] Moods = [
        ("none", "–", "log.export.mood.none", "None"),
        ("exclamation", "❗", "log.export.mood.exclamation", "Alert"),
        ("skull", "💀", "log.export.mood.skull", "Skull"),
        ("thumb_down", "👎", "log.export.mood.thumbDown", "Down"),
        ("sad", "😢", "log.export.mood.sad", "Sad"),
        ("question", "❓", "log.export.mood.question", "Question"),
        ("heart", "❤", "log.export.mood.heart", "Heart"),
        ("thumb_up", "👍", "log.export.mood.thumbUp", "Up"),
        ("happy", "☺", "log.export.mood.happy", "Happy"),
    ];

    static readonly FeedbackCategory[] Categories = [
        new("bug", "Bug", "log.export.category.bug.desc", "This is a bug", new Color(0.45f, 0.85f, 0.72f)),
        new("bug_maybe", "bug?", "log.export.category.bugMaybe.desc", "Maybe a bug", new Color(1f, 0.62f, 0.22f)),
        new("balance", "Balance", "log.export.category.balance.desc", "Numbers or strength feel off", new Color(0.75f, 0.82f, 0.95f)),
        new("feedback", "Feedback", "log.export.category.feedback.desc", "Suggestion or idea", new Color(0.95f, 0.84f, 0.45f)),
        new("translation", "Translation", "log.export.category.translation.desc", "Wrong or missing text", new Color(0.72f, 0.78f, 0.98f)),
        new("ui", "UI", "log.export.category.ui.desc", "Layout or visuals look wrong", new Color(0.65f, 0.88f, 0.95f)),
        new("multiplayer", "Multiplayer", "log.export.category.multiplayer.desc", "Online play issue", new Color(0.85f, 0.7f, 0.95f)),
    ];

    readonly record struct FeedbackCategory(
        string Id,
        string Title,
        string DescKey,
        string DescFallback,
        Color IconColor);

    static string FormatCategoryLabel(FeedbackCategory cat) =>
        $"{cat.Title} - {I18N.T(cat.DescKey, cat.DescFallback)}";

    static MdiIcon CategoryIcon(FeedbackCategory cat) => cat.Id switch {
        "bug" => MdiIcon.From("bug"),
        "bug_maybe" => MdiIcon.From("alert"),
        "balance" => MdiIcon.From("scale-balance"),
        "feedback" => MdiIcon.From("comment-quote-outline"),
        "translation" => MdiIcon.From("translate"),
        "ui" => MdiIcon.From("monitor-screenshot"),
        "multiplayer" => MdiIcon.From("account-group"),
        _ => MdiIcon.From("help-circle-outline"),
    };

    internal static void BuildContent(VBoxContainer vbox, bool compact = false) {
        vbox.AddThemeConstantOverride("separation", compact ? 8 : 10);

        if (!compact) {
            var titleBox = new VBoxContainer();
            titleBox.AddThemeConstantOverride("separation", 4);
            titleBox.AddChild(DevPanelUI.CreatePanelTitle(I18N.T("log.export.title", "Log Export")));
            var subtitle = new Label {
                Text = I18N.T("log.export.subtitle",
                    "Describe the issue, attach a screenshot, and export a ZIP for mod authors."),
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
        var defaultLogIdx = ResolveDefaultLogIndex(logFiles);
        bool inRun = RunManager.Instance?.IsInProgress == true;
        OptionButton? logOption = null;

        if (!inRun) {
            logOption = BuildLogDropdown(logFiles, compact);
            var logRow = new VBoxContainer();
            logRow.AddThemeConstantOverride("separation", 4);
            logRow.AddChild(MakeFieldLabel(I18N.T("log.export.log.label", "Game log file")));
            logRow.AddChild(logOption);
            vbox.AddChild(logRow);
        }

        if (logFiles.Count == 0) {
            var noLogHint = new Label {
                Text = I18N.T("log.export.log.missing", "No game log file found under user://logs/."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            noLogHint.AddThemeFontSizeOverride("font_size", 10);
            noLogHint.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.45f));
            vbox.AddChild(noLogHint);
        }

        vbox.AddChild(MakeFieldLabel(I18N.T("log.export.category.label", "Category")));
        string categoryId = Categories[0].Id;
        var categoryGroup = new ButtonGroup();
        var categoryRow = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        categoryRow.AddThemeConstantOverride("h_separation", 8);
        categoryRow.AddThemeConstantOverride("v_separation", 6);
        foreach (var cat in Categories) {
            var btn = DevPanelUI.CreateFilterChip(FormatCategoryLabel(cat), cat.Id == categoryId);
            btn.ButtonGroup = categoryGroup;
            btn.Icon = CategoryIcon(cat).Texture(16, cat.IconColor);
            btn.IconAlignment = HorizontalAlignment.Left;
            btn.Alignment = HorizontalAlignment.Left;
            btn.AddThemeConstantOverride("h_separation", 6);
            var captured = cat.Id;
            btn.Pressed += () => categoryId = captured;
            categoryRow.AddChild(btn);
        }
        vbox.AddChild(categoryRow);

        vbox.AddChild(MakeFieldLabel(I18N.T("log.export.mood.label", "Reaction")));
        var moodRow = new HBoxContainer();
        moodRow.AddThemeConstantOverride("separation", 4);
        var moodButtons = new List<Button>();
        string moodId = "none";
        foreach (var mood in Moods) {
            var btn = new Button {
                Text = mood.Glyph,
                TooltipText = I18N.T(mood.Key, mood.Fallback),
                ToggleMode = true,
                ButtonPressed = mood.Id == "none",
                CustomMinimumSize = new Vector2(32, 28),
                FocusMode = Control.FocusModeEnum.None,
                Flat = true
            };
            btn.AddThemeFontSizeOverride("font_size", 14);
            var captured = mood.Id;
            btn.Pressed += () => {
                moodId = captured;
                foreach (var other in moodButtons)
                    other.ButtonPressed = other == btn;
            };
            moodButtons.Add(btn);
            moodRow.AddChild(btn);
        }
        vbox.AddChild(moodRow);

        vbox.AddChild(MakeFieldLabel(I18N.T("log.export.description.label", "What happened")));
        var description = new TextEdit {
            CustomMinimumSize = new Vector2(0, compact ? 64 : 88),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
            PlaceholderText = I18N.T(
                "log.export.description.placeholder",
                "Steps to reproduce, expected vs actual…")
        };
        description.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(description);

        var screenshotToggle = new CheckButton {
            Text = I18N.T("log.export.screenshot.label", "Include screenshot"),
            ButtonPressed = true,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            FocusMode = Control.FocusModeEnum.None
        };
        screenshotToggle.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(screenshotToggle);

        var extra = new List<FeedbackReportBuilder.NamedBlob>();
        var extraLabel = new Label {
            Text = ExtraCountText(0),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        extraLabel.AddThemeFontSizeOverride("font_size", 10);
        extraLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);

        var extraRow = new HBoxContainer();
        extraRow.AddThemeConstantOverride("separation", 6);
        var addBtn = SmallActionButton(I18N.T("log.export.images.add", "Add images"), MdiIcon.FolderOpen);
        var pasteBtn = SmallActionButton(I18N.T("log.export.images.paste", "Paste"), MdiIcon.ContentCopy);
        extraRow.AddChild(addBtn);
        extraRow.AddChild(pasteBtn);
        vbox.AddChild(extraRow);
        vbox.AddChild(extraLabel);

        addBtn.Pressed += () => OpenImagePicker(vbox, extra, extraLabel);
        pasteBtn.Pressed += () => TryPasteClipboardImage(extra, extraLabel);

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
            int logIdx = inRun ? defaultLogIdx : logOption!.Selected;
            if (logFiles.Count == 0 || logIdx < 0 || logIdx >= logFiles.Count)
                return;
            var reqBase = new FeedbackReportBuilder.BuildRequest(
                LogFilePath: logFiles[logIdx].AbsPath,
                Description: description.Text?.Trim(),
                Category: categoryId,
                Mood: moodId,
                ExtraImages: extra.Count == 0 ? null : extra.ToArray());

            TaskHelper.RunSafely(RunFormExport(
                reqBase,
                screenshotToggle.ButtonPressed,
                exportBtn,
                statusLabel,
                logFiles.Count == 0));
        };
    }

    internal static async Task<(string? ZipPath, string? Error)> ExportDefaultZipAsync(
        byte[]? screenshotPng = null) {
        LogCollector.RefreshFileSnapshot();
        var logs = FeedbackReportBuilder.ScanLogFiles();
        if (logs.Count == 0)
            return (null, null);

        int idx = ResolveDefaultLogIndex(logs);
        var req = new FeedbackReportBuilder.BuildRequest(
            LogFilePath: logs[idx].AbsPath,
            Category: "bug",
            ScreenshotPng: screenshotPng);

        FeedbackReportBuilder.FlushOfficialReplay();
        return await Task.Run(() => {
            try {
                return ((string?)FeedbackReportBuilder.Build(req), (string?)null);
            }
            catch (Exception ex) {
                return ((string?)null, ex.Message);
            }
        });
    }

    static async Task RunFormExport(
        FeedbackReportBuilder.BuildRequest req,
        bool includeScreenshot,
        Button btn,
        Label statusLabel,
        bool noLogs) {
        btn.Disabled = true;
        btn.Text = I18N.T("log.export.exporting", "Generating…");
        statusLabel.Visible = false;

        byte[]? shot = null;
        if (includeScreenshot)
            shot = await FeedbackScreenshotCapture.TryCapturePngAsync();

        req = req with { ScreenshotPng = shot };

        FeedbackReportBuilder.FlushOfficialReplay();
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

        if (!GodotObject.IsInstanceValid(btn))
            return;

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

    static void OpenImagePicker(
        Control host,
        List<FeedbackReportBuilder.NamedBlob> extra,
        Label extraLabel) {
        if (!GodotObject.IsInstanceValid(host) || !host.IsInsideTree())
            return;

        var dlg = new FileDialog {
            FileMode = FileDialog.FileModeEnum.OpenFiles,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Title = I18N.T("log.export.images.add", "Add images"),
            CurrentDir = OS.GetSystemDir(OS.SystemDir.Pictures)
        };
        dlg.AddFilter("*.png,*.jpg,*.jpeg,*.webp", I18N.T("log.export.images.filter", "Images"));
        host.AddChild(dlg);
        dlg.FilesSelected += paths => {
            foreach (var path in paths) {
                var blob = FeedbackReportBuilder.TryReadImageFile(path);
                if (blob != null)
                    extra.Add(blob.Value);
            }
            extraLabel.Text = ExtraCountText(extra.Count);
            dlg.QueueFree();
        };
        dlg.Canceled += () => dlg.QueueFree();
        dlg.PopupCentered();
    }

    static void TryPasteClipboardImage(
        List<FeedbackReportBuilder.NamedBlob> extra,
        Label extraLabel) {
        if (!DisplayServer.ClipboardHasImage())
            return;
        var img = DisplayServer.ClipboardGetImage();
        if (img == null)
            return;
        var bytes = img.SavePngToBuffer();
        if (bytes == null || bytes.Length == 0)
            return;
        extra.Add(new FeedbackReportBuilder.NamedBlob($"clipboard-{extra.Count + 1}.png", bytes));
        extraLabel.Text = ExtraCountText(extra.Count);
    }

    static string ExtraCountText(int count) =>
        count == 0
            ? I18N.T("log.export.images.none", "No extra images")
            : I18N.T("log.export.images.count", "{0} extra image(s)", count);

    static Button SmallActionButton(string text, MdiIcon icon) {
        var btn = new Button {
            Text = text,
            Icon = icon.Texture(14, Colors.White),
            CustomMinimumSize = new Vector2(0, 28),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None
        };
        btn.AddThemeFontSizeOverride("font_size", 11);
        return btn;
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
            "report-meta.json — " + I18N.T("log.export.contents.meta", "Description, category, loaded mods"),
            "screenshot.png — " + I18N.T("log.export.contents.screenshot", "Game screenshot (if enabled)"),
            "attachments/ — " + I18N.T("log.export.contents.attachments", "Extra images"),
            "combat-checkpoint/ — " + I18N.T("log.export.contents.checkpoint", "Last combat snapshot, if any"),
            "saves/ — " + I18N.T("log.export.contents.saves", "Official profile saves, current run, and latest.mcr replay"),
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
