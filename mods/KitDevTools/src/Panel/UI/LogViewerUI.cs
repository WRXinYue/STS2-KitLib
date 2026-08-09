using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using KitLib;
using KitLib.Icons;
using KitLib.Modding;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

/// <summary>
/// Log viewer spliced to the DevMode rail (fixed-width browser panel + backdrop, same pattern as Console).
/// Filters live in a left scroll sidebar (level, text, mod source, suppress rules); log center; stats + pie on the right.
/// </summary>
internal static class LogViewerUI {
    private const string RootName = "KitLibLogViewer";
    private const string DualMetaKey = "dm_dual_log_viewer";
    private const string CarrierNodeName = "LogViewerDualCarrier";
    private const float PanelW = 880f;
    private const float LogExportExtWidth = 300f;
    private const float FilterSideMinW = 216f;
    private const float StatsSideMinW = 164f;

    // BBCode hex colors per level
    private const string ColInfo = "#C8C8DC";
    private const string ColWarn = "#FFC840";
    private const string ColError = "#FF5F5F";
    private const string ColDebug = "#6A6A8A";
    private const string ColTime = "#55556A";
    private const float GameSourceDimAmount = 0.18f;

    private static Label CreateLogFilterSectionHeading(string i18nKey, string fallback) {
        var l = new Label { Text = I18N.T(i18nKey, fallback) };
        l.AddThemeFontSizeOverride("font_size", 10);
        l.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return l;
    }

    public static void Show(NGlobalUi globalUi, bool expandLogExport = false) {
        var parent = (Node)globalUi;
        Remove(parent);
        void Close() => DevPanelUI.RequestCloseBrowserOverlay(globalUi, RootName, () => Remove(parent));
        LogCollector.RefreshFileSnapshot();
        var dual = DevPanelUI.CreateDualColumnOverlay(new DevPanelUI.DualColumnOverlayOptions {
            GlobalUi = globalUi,
            RootName = RootName,
            DualMetaKey = DualMetaKey,
            CarrierNodeName = CarrierNodeName,
            MainDefaultWidth = PanelW,
            ExtDefaultWidth = LogExportExtWidth,
            FallbackClose = Close,
        });
        dual.MainContent.AddThemeConstantOverride("separation", 8);
        LogSourcePieChart? pieChart = null;
        var exportBtn = BuildPanel(dual.MainContent, dual.Root, Close, chart => pieChart = chart);
        WireLogExportExtension(dual, exportBtn);
        dual.AttachToScene();
        pieChart?.RefreshAfterOverlayOpen();
        if (expandLogExport)
            dual.OpenExtension();
    }

    public static void ShowOnMainMenu(NMainMenu mainMenu, bool expandLogExport = false) {
        var parent = mainMenu.GetTree().Root;
        HideAnywhere();
        Action close = HideAnywhere;
        LogCollector.AcknowledgeAlerts();
        LogCollector.RefreshFileSnapshot();
        var (root, vbox, mainPanel) = DevMainMenuOverlay.CreateWithPanel(
            parent, RootName, PanelW, close, contentSeparation: 8);
        var exportBtn = BuildPanel(vbox, root, close);
        WireMainMenuLogExportExtension(root, mainPanel, exportBtn, expandLogExport);
    }

    private static Button BuildPanel(
        VBoxContainer vbox,
        Control root,
        Action onClose,
        Action<LogSourcePieChart>? capturePieChart = null) {
        // ── Header ──
        var exportBtn = BuildHeader(vbox, onClose);

        // ── Level / search / rule chips (wired below; placed in filter sidebar) ──
        var chipAll = DevPanelUI.CreateFilterChip(I18N.T("log.filter.all", "All"), active: true);
        var chipInfo = DevPanelUI.CreateFilterChip(I18N.T("log.filter.info", "≥ Info"), active: false);
        var chipWarn = DevPanelUI.CreateFilterChip(I18N.T("log.filter.warn", "≥ Warn"), active: false);
        var chipError = DevPanelUI.CreateFilterChip(I18N.T("log.filter.error", "Error"), active: false);

        var (searchRow, searchInput) = DevPanelUI.CreateSearchRow(I18N.T("log.search", "Filter text..."));
        searchRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var ruleChips = new Button[LogSuppressor.BuiltInRules.Length];
        for (int i = 0; i < LogSuppressor.BuiltInRules.Length; i++) {
            var rule = LogSuppressor.BuiltInRules[i];
            var chip = DevPanelUI.CreateFilterChip(rule.Pattern, active: rule.Enabled);
            chip.TooltipText = rule.Description;
            chip.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            ruleChips[i] = chip;
        }

        // ── Body: filter sidebar | log | stats ────────────────────────────
        var bodyHBox = new HBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        bodyHBox.AddThemeConstantOverride("separation", 10);

        var emptyBox = new StyleBoxEmpty();

        var filterScroll = new ScrollContainer {
            CustomMinimumSize = new Vector2(FilterSideMinW, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            ClipContents = true,
        };
        filterScroll.AddThemeStyleboxOverride("panel", emptyBox);
        filterScroll.AddThemeStyleboxOverride("focus", emptyBox);

        var filterInner = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        filterInner.AddThemeConstantOverride("separation", 10);

        var filterTitle = new Label { Text = I18N.T("log.filter.sidebar", "Filters") };
        filterTitle.AddThemeFontSizeOverride("font_size", 12);
        filterTitle.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        filterInner.AddChild(filterTitle);

        filterInner.AddChild(CreateLogFilterSectionHeading("log.filter.section.level", "Level"));
        var levelVBox = new VBoxContainer();
        levelVBox.AddThemeConstantOverride("separation", 6);
        foreach (var c in new[] { chipAll, chipInfo, chipWarn, chipError }) {
            c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            levelVBox.AddChild(c);
        }

        filterInner.AddChild(levelVBox);

        filterInner.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.ButtonBgNormal,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });

        filterInner.AddChild(CreateLogFilterSectionHeading("log.filter.section.text", "Text"));
        filterInner.AddChild(searchRow);

        filterInner.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.ButtonBgNormal,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });

        filterInner.AddChild(CreateLogFilterSectionHeading("log.filter.section.mods", "Mod source"));
        var modFilterVBox = new VBoxContainer();
        modFilterVBox.AddThemeConstantOverride("separation", 4);
        filterInner.AddChild(modFilterVBox);

        filterInner.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.ButtonBgNormal,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });

        filterInner.AddChild(CreateLogFilterSectionHeading("log.filter.section.rules", "Suppress rules"));
        var rulesVBox = new VBoxContainer();
        rulesVBox.AddThemeConstantOverride("separation", 4);
        foreach (var chip in ruleChips)
            rulesVBox.AddChild(chip);

        filterInner.AddChild(rulesVBox);
        filterScroll.AddChild(filterInner);
        bodyHBox.AddChild(filterScroll);

        bodyHBox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(1, 0),
            Color = KitLibTheme.ButtonBgNormal,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        });

        var logColumn = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };

        var logHost = new Control {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ClipContents = false,
            ClipChildren = Control.ClipChildrenMode.Disabled,
        };

        // RichTextLabel scrolls internally — avoids ScrollContainer↔min-height resize feedback loops
        // (those could freeze the main thread on click / layout with no managed exception).
        var richText = new RichTextLabel {
            BbcodeEnabled = true,
            SelectionEnabled = true,
            ScrollActive = true,
            ScrollFollowing = false,
            FitContent = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ContextMenuEnabled = true,
        };
        richText.AddThemeFontSizeOverride("normal_font_size", 12);
        var rtlNoBg = new StyleBoxEmpty();
        richText.AddThemeStyleboxOverride("normal", rtlNoBg);
        richText.AddThemeStyleboxOverride("focus", rtlNoBg);
        richText.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        richText.OffsetLeft = 0;
        richText.OffsetTop = 0;
        richText.OffsetRight = 0;
        richText.OffsetBottom = 0;

        var copyFloatBtn = new Button {
            Name = "LogCopyFloat",
            FocusMode = Control.FocusModeEnum.None,
            Flat = true,
            Icon = MdiIcon.ContentCopy.Texture(16, KitLibTheme.Subtle),
            Visible = false,
            ZIndex = 2,
            TooltipText = I18N.T("log.copyAll", "Copy All"),
            CustomMinimumSize = new Vector2(44, 36),
            Alignment = HorizontalAlignment.Center,
            IconAlignment = HorizontalAlignment.Center,
            ExpandIcon = false,
            ClipContents = false,
        };
        copyFloatBtn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        copyFloatBtn.OffsetLeft = -58;
        copyFloatBtn.OffsetRight = -10;
        copyFloatBtn.OffsetTop = 6;
        copyFloatBtn.OffsetBottom = 42;
        ApplySmallFlatButton(copyFloatBtn);
        foreach (var key in new[] { "normal", "hover", "pressed", "focus" }) {
            if (copyFloatBtn.GetThemeStylebox(key) is StyleBoxFlat box) {
                const int pad = 6;
                box.ContentMarginLeft = pad;
                box.ContentMarginRight = pad;
                box.ContentMarginTop = pad;
                box.ContentMarginBottom = pad;
            }
        }

        copyFloatBtn.AddThemeConstantOverride("icon_max_width", 24);
        copyFloatBtn.AddThemeConstantOverride("icon_max_height", 24);

        logHost.AddChild(richText);
        logHost.AddChild(copyFloatBtn);
        logColumn.AddChild(logHost);
        bodyHBox.AddChild(logColumn);

        void SetCopyFloatDefaultIcon()
            => copyFloatBtn.Icon = MdiIcon.ContentCopy.Texture(16, KitLibTheme.Subtle);

        void HideCopyFloat() {
            copyFloatBtn.Visible = false;
            SetCopyFloatDefaultIcon();
        }

        void ShowCopyFloat() => copyFloatBtn.Visible = true;
        richText.MouseEntered += ShowCopyFloat;
        copyFloatBtn.MouseEntered += ShowCopyFloat;
        richText.MouseExited += () => {
            Callable.From(() => {
                var h = logHost.GetViewport().GuiGetHoveredControl();
                if (h != copyFloatBtn)
                    HideCopyFloat();
            }).CallDeferred();
        };
        copyFloatBtn.MouseExited += () => {
            Callable.From(() => {
                var h = logHost.GetViewport().GuiGetHoveredControl();
                if (h != richText)
                    HideCopyFloat();
            }).CallDeferred();
        };
        string lastPlainText = "";

        copyFloatBtn.Pressed += () => {
            DisplayServer.ClipboardSet(lastPlainText);
            copyFloatBtn.Icon = MdiIcon.Check.Texture(16, KitLibTheme.Accent);
            var t = copyFloatBtn.GetTree().CreateTimer(1.15);
            t.Timeout += () => {
                if (!GodotObject.IsInstanceValid(copyFloatBtn)) return;
                SetCopyFloatDefaultIcon();
            };
        };

        bodyHBox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(1, 0),
            Color = KitLibTheme.ButtonBgNormal,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        });

        var statsColumn = new VBoxContainer {
            CustomMinimumSize = new Vector2(StatsSideMinW, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ClipContents = false,
        };
        statsColumn.AddThemeConstantOverride("separation", 6);

        var statsTitle = new Label { Text = I18N.T("log.stats.title", "来源统计") };
        statsTitle.AddThemeFontSizeOverride("font_size", 12);
        statsTitle.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        statsColumn.AddChild(statsTitle);

        var pieHost = new CenterContainer {
            CustomMinimumSize = new Vector2(0, 148),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        var pieChart = new LogSourcePieChart();
        pieHost.AddChild(pieChart);
        statsColumn.AddChild(pieHost);
        capturePieChart?.Invoke(pieChart);

        var statsScroll = new ScrollContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            ClipContents = true,
        };
        statsScroll.AddThemeStyleboxOverride("panel", emptyBox);
        statsScroll.AddThemeStyleboxOverride("focus", emptyBox);

        var statsVBox = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        statsVBox.AddThemeConstantOverride("separation", 4);
        statsScroll.AddChild(statsVBox);
        statsColumn.AddChild(statsScroll);

        bodyHBox.AddChild(statsColumn);

        vbox.AddChild(bodyHBox);

        // ── Footer: entry count ──
        var footerRow = new HBoxContainer();
        footerRow.AddThemeConstantOverride("separation", 8);

        var countLabel = new Label { Text = "" };
        countLabel.AddThemeFontSizeOverride("font_size", 11);
        countLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        footerRow.AddChild(countLabel);
        footerRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        vbox.AddChild(footerRow);

        // ── State ──
        LogLevel? minLevel = null;
        string textFilter = "";
        var modVisible = new Dictionary<string, bool>(StringComparer.Ordinal);
        var modChips = new Dictionary<string, Button>(StringComparer.Ordinal);
        var lastSyncedModIds = new HashSet<string>(StringComparer.Ordinal);

        void PublishFilters(HashSet<string>? loaded = null, Dictionary<string, string>? aliases = null) {
            loaded ??= ModRuntime.Catalog.GetIdSet();
            aliases ??= LogModSourceResolver.BuildAliasLookup(loaded);
            LogViewerFilterSync.Publish(minLevel, textFilter, modVisible, loaded, aliases);
            lastSyncedModIds = new HashSet<string>(loaded, StringComparer.Ordinal);
        }
        void SyncModFilterChips(HashSet<string> loadedModIds) {
            var discovered = new HashSet<string>(StringComparer.Ordinal) { "Game", "KitLib" };
            foreach (var id in loadedModIds)
                discovered.Add(id);

            foreach (var key in modChips.Keys.ToArray()) {
                if (discovered.Contains(key)) continue;
                modChips[key].QueueFree();
                modChips.Remove(key);
                modVisible.Remove(key);
            }

            var ordered = discovered
                .OrderBy(k => k == "Game" ? 0 : 1)
                .ThenBy(k => k, StringComparer.Ordinal)
                .ToList();

            foreach (var id in ordered) {
                if (modChips.ContainsKey(id))
                    continue;

                if (!modVisible.ContainsKey(id))
                    modVisible[id] = true;

                var chip = DevPanelUI.CreateFilterChip(ShortModLabel(id), active: modVisible[id]);
                chip.TooltipText = id;
                chip.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                var capturedId = id;
                chip.Toggled += v => {
                    modVisible[capturedId] = v;
                    PublishFilters();
                    Repopulate();
                };
                modChips[id] = chip;
                modFilterVBox.AddChild(chip);
            }
        }

        const double ScrollFollowThreshold = 12.0;
        // User intent: keep pinned to latest lines (not inferred from scrollbar position on open).
        bool stickToBottom = true;
        bool programmaticScroll = false;

        bool IsAtScrollBottom() {
            var bar = richText.GetVScrollBar();
            if (bar == null || bar.MaxValue <= 0)
                return true;
            return bar.Value >= bar.MaxValue - ScrollFollowThreshold;
        }

        void ScrollToBottom() {
            programmaticScroll = true;
            try {
                int lines = richText.GetLineCount();
                if (lines > 0)
                    richText.ScrollToLine(lines - 1);
                var bar = richText.GetVScrollBar();
                if (bar != null)
                    bar.Value = bar.MaxValue;
            }
            finally {
                programmaticScroll = false;
            }
        }

        void RestoreScrollAfterRepopulate(bool followTail, double prevScroll, double prevMax) {
            if (followTail) {
                ScrollToBottom();
                return;
            }

            var bar = richText.GetVScrollBar();
            if (bar == null || prevMax <= 0)
                return;

            bar.Value = Math.Clamp(prevScroll / prevMax * bar.MaxValue, 0, bar.MaxValue);
        }

        var tailScrollBar = richText.GetVScrollBar();
        if (tailScrollBar != null) {
            tailScrollBar.ValueChanged += _ => {
                if (programmaticScroll)
                    return;
                stickToBottom = IsAtScrollBottom();
            };
        }

        string BuildBbCode(
            List<FilteredLogEntry> entries,
            HashSet<string> loadedModIds,
            Dictionary<string, string> modIdAliases) {
            var sb = new StringBuilder(entries.Count * 96);
            string boundaryCol = LogSourceColors.ColorToBbHex(KitLibTheme.Accent);

            foreach (var (entry, source) in entries) {
                if (LogCollector.IsSessionBoundary(entry)) {
                    sb.Append($"[color={boundaryCol}]──── {LogCollector.SessionBoundaryMarker} ────[/color]\n");
                    continue;
                }

                if (entry.IsFromFile) {
                    AppendHistoricalBbCodeLine(sb, entry);
                    continue;
                }

                bool dim = source == "Game";
                string timeCol = dim
                    ? LogSourceColors.DimBbHex(ColTime, GameSourceDimAmount)
                    : ColTime;
                string levelCol = dim
                    ? LogSourceColors.DimBbHex(LevelColor(entry.Level), GameSourceDimAmount)
                    : LevelColor(entry.Level);
                string time = entry.HasWallClockTime
                    ? entry.Time.ToString("HH:mm:ss")
                    : "--:--:--";
                sb.Append($"[color={timeCol}]{time}[/color] ");
                AppendLiveEntryBody(sb, entry, source, levelCol, loadedModIds, modIdAliases);
                sb.Append('\n');
            }

            return sb.ToString();
        }

        static string BuildPlainText(List<FilteredLogEntry> entries) {
            var sb = new StringBuilder(entries.Count * 80);
            foreach (var (entry, _) in entries) {
                if (LogCollector.IsSessionBoundary(entry)) {
                    sb.Append("──── ").Append(LogCollector.SessionBoundaryMarker).Append(" ────\n");
                    continue;
                }

                if (entry.IsFromFile) {
                    sb.Append("--:--:-- ").AppendLine(entry.Text);
                    continue;
                }

                string time = entry.HasWallClockTime
                    ? entry.Time.ToString("HH:mm:ss")
                    : "--:--:--";
                sb.Append(time).Append(' ').Append(LevelBadge(entry.Level)).Append(' ').AppendLine(entry.Text);
            }

            return sb.ToString();
        }

        void Repopulate() {
            LogCollector.RefreshFileSnapshotIfChanged();
            var scrollBar = richText.GetVScrollBar();
            bool followTail = stickToBottom;
            double prevScroll = scrollBar?.Value ?? 0;
            double prevMax = scrollBar?.MaxValue ?? 0;

            var entries = LogCollector.GetSnapshot();
            LogCollector.MarkClean();

            LogSuppressor.ResetCounts();
            var loadedModIds = ModRuntime.Catalog.GetIdSet();
            var modIdAliases = LogModSourceResolver.BuildAliasLookup(loadedModIds);
            SyncModFilterChips(loadedModIds);
            var (filtered, suppressed, modStats) = FilterEntries(
                entries, minLevel, textFilter, modVisible, loadedModIds, modIdAliases);

            richText.Text = BuildBbCode(filtered, loadedModIds, modIdAliases);
            lastPlainText = BuildPlainText(filtered);

            // Update count label
            countLabel.Text = suppressed > 0
                ? I18N.T("log.count.withNoise", "{0} entries  •  {1} filtered", filtered.Count, suppressed)
                : I18N.T("log.count", "{0} entries", filtered.Count);

            // Update rule chip labels with hit counts
            for (int i = 0; i < LogSuppressor.BuiltInRules.Length; i++) {
                var rule = LogSuppressor.BuiltInRules[i];
                var chip = ruleChips[i];
                // Short label: first word(s) of pattern + count
                string shortLabel = ShortRuleLabel(rule.Pattern);
                chip.Text = rule.HitCount > 0
                    ? $"{shortLabel} ({rule.HitCount})"
                    : shortLabel;
            }

            // Update stats panel + pie chart
            pieChart.SetData(modStats);
            RefreshStatsPanel(statsVBox, modStats);
            if (!loadedModIds.SetEquals(lastSyncedModIds))
                PublishFilters(loadedModIds, modIdAliases);

            Callable.From(() => RestoreScrollAfterRepopulate(followTail, prevScroll, prevMax)).CallDeferred();
        }

        for (int i = 0; i < LogSuppressor.BuiltInRules.Length; i++) {
            var capturedRule = LogSuppressor.BuiltInRules[i];
            ruleChips[i].Toggled += v => {
                capturedRule.Enabled = v;
                PublishFilters();
                Repopulate();
            };
        }

        // ── Level chip logic ──
        void SetMinLevel(LogLevel? level) {
            minLevel = level;
            chipAll.ButtonPressed = level == null;
            chipInfo.ButtonPressed = level == LogLevel.Info;
            chipWarn.ButtonPressed = level == LogLevel.Warn;
            chipError.ButtonPressed = level == LogLevel.Error;
            PublishFilters();
            Repopulate();
        }

        chipAll.Pressed += () => SetMinLevel(null);
        chipInfo.Pressed += () => SetMinLevel(LogLevel.Info);
        chipWarn.Pressed += () => SetMinLevel(LogLevel.Warn);
        chipError.Pressed += () => SetMinLevel(LogLevel.Error);

        searchInput.TextChanged += t => { textFilter = t; PublishFilters(); Repopulate(); };

        void OnClear() { LogCollector.Clear(); Repopulate(); }
        BuildHeaderClearWire(vbox, OnClear);

        // ── Auto-refresh timer (1 s) ──
        var timer = new Godot.Timer { WaitTime = 1.0, Autostart = true };
        timer.Timeout += () => {
            if (LogCollector.IsDirty)
                Repopulate();
        };
        root.AddChild(timer);

        void ScheduleInitialRepopulate() {
            if (root.IsInsideTree()) {
                Callable.From(Repopulate).CallDeferred();
                return;
            }

            void OnTreeEntered() {
                root.TreeEntered -= OnTreeEntered;
                Callable.From(Repopulate).CallDeferred();
            }

            root.TreeEntered += OnTreeEntered;
        }

        ScheduleInitialRepopulate();
        return exportBtn;
    }

    public static void Remove(NGlobalUi globalUi) => Remove((Node)globalUi);

    public static void Remove(Node parent) => HideAnywhere();

    public static void HideAnywhere() => DevMainMenuOverlay.RemoveAnywhere(RootName);

    // ── Filtering ────────────────────────────────────────────────────────

    private readonly record struct FilteredLogEntry(LogCollector.Entry Entry, string Source);

    private static (List<FilteredLogEntry> filtered, int suppressed, Dictionary<string, int> modStats)
        FilterEntries(
            List<LogCollector.Entry> entries,
            LogLevel? minLevel,
            string textFilter,
            Dictionary<string, bool> modSourceFilter,
            HashSet<string> loadedModIds,
            Dictionary<string, string> modIdAliases) {
        var result = new List<FilteredLogEntry>(entries.Count);
        var modStats = new Dictionary<string, int>(StringComparer.Ordinal);
        int suppressed = 0;
        bool inSuppressedNativeBlock = false;

        foreach (var e in entries) {
            if (LogCollector.IsSessionBoundary(e)) {
                inSuppressedNativeBlock = false;
                result.Add(new FilteredLogEntry(e, "KitLib"));
                continue;
            }

            // A suppressed Godot native error (e.g. "ERROR:") is followed by indented "at:"/
            // "C# backtrace"/"[n] ..." lines; swallow them so the whole block hides together.
            if (inSuppressedNativeBlock && IsNativeErrorContinuation(e.Text)) {
                suppressed++;
                continue;
            }
            inSuppressedNativeBlock = false;

            if (minLevel != null && e.Level < minLevel.Value) continue;
            if (!string.IsNullOrWhiteSpace(textFilter) &&
                !e.Text.Contains(textFilter, StringComparison.OrdinalIgnoreCase)) continue;

            // Pre-boundary file history: plain display only — no mod attribution, filters, or stats.
            if (e.IsFromFile) {
                result.Add(new FilteredLogEntry(e, ""));
                continue;
            }

            string source = ParseSource(e.Text, loadedModIds, modIdAliases);
            if (modSourceFilter.TryGetValue(source, out var showMod) && !showMod)
                continue;

            if (LogSuppressor.IsSuppressed(e.Text)) {
                suppressed++;
                inSuppressedNativeBlock = IsNativeErrorHeader(e.Text);
                continue;
            }

            result.Add(new FilteredLogEntry(e, source));

            modStats.TryGetValue(source, out int n);
            modStats[source] = n + 1;
        }
        return (result, suppressed, modStats);
    }

    /// <summary>True for a Godot native error/warning header whose indented detail lines should collapse with it.</summary>
    private static bool IsNativeErrorHeader(string text)
        => text.StartsWith("ERROR:", StringComparison.Ordinal)
           || text.StartsWith("WARNING:", StringComparison.Ordinal);

    /// <summary>Godot prints error detail ("at:", "C# backtrace", "[n] …") indented under the header line.</summary>
    private static bool IsNativeErrorContinuation(string text)
        => text.Length > 0 && char.IsWhiteSpace(text[0]);

    /// <summary>
    /// Resolves log source by matching bracket tags against <see cref="ModRuntime.Catalog"/> ids.
    /// Walks <c>[...]</c> segments from the left and returns the first whose inner text matches a loaded mod id
    /// (exact or normalized via <see cref="LogModSourceResolver.NormalizeIdKey"/>); otherwise "Game".
    /// </summary>
    private static string ParseSource(
        string text,
        HashSet<string> loadedModIds,
        Dictionary<string, string> modIdAliases) {
        if (loadedModIds.Count == 0 || string.IsNullOrEmpty(text))
            return "Game";

        return TryFindModTagSpan(text, loadedModIds, modIdAliases, out _, out _, out var modId)
            ? modId
            : "Game";
    }

    private static bool TryFindModTagSpan(
        string text,
        HashSet<string> loadedModIds,
        Dictionary<string, string> modIdAliases,
        out int tagStart,
        out int tagEndExclusive,
        out string modId) {
        tagStart = 0;
        tagEndExclusive = 0;
        modId = "";

        if (loadedModIds.Count == 0 || string.IsNullOrEmpty(text))
            return false;

        int i = 0;
        while (i < text.Length) {
            int open = text.IndexOf('[', i);
            if (open < 0) break;
            int close = text.IndexOf(']', open + 1);
            if (close <= open + 1) {
                i = open + 1;
                continue;
            }

            string inner = text.Substring(open + 1, close - open - 1);
            if (TryResolveModId(inner, loadedModIds, modIdAliases, out modId)) {
                tagStart = open;
                tagEndExclusive = close + 1;
                return true;
            }

            i = close + 1;
        }

        return false;
    }

    private static readonly HashSet<string> LogLevelBracketTags = new(StringComparer.OrdinalIgnoreCase) {
        "INFO", "WARN", "WARNING", "ERROR", "DEBUG", "LOAD", "VERYDEBUG", "VDB", "DBG",
    };

    private static bool TryResolveModId(
        string candidate,
        HashSet<string> loadedModIds,
        Dictionary<string, string> modIdAliases,
        out string modId) {
        modId = "";

        if (LogLevelBracketTags.Contains(candidate))
            return false;

        if (!LooksLikeModOrScopeTag(candidate))
            return false;

        if (loadedModIds.Contains(candidate)) {
            modId = candidate;
            return true;
        }

        if (TryResolveModIdKey(LogModSourceResolver.NormalizeIdKey(candidate), modIdAliases, out modId))
            return true;

        int lastDot = candidate.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < candidate.Length - 1 &&
            TryResolveModIdKey(LogModSourceResolver.NormalizeIdKey(candidate[(lastDot + 1)..]), modIdAliases, out modId))
            return true;

        return false;
    }

    private static bool TryResolveModIdKey(
        string normalizedKey,
        Dictionary<string, string> modIdAliases,
        out string modId) {
        if (modIdAliases.TryGetValue(normalizedKey, out var resolved) && resolved != null) {
            modId = resolved;
            return true;
        }

        modId = "";
        return false;
    }

    /// <summary>
    /// Pre-boundary disk history: dim single-color line, no mod tag highlighting or BBCode splitting.
    /// </summary>
    private static void AppendHistoricalBbCodeLine(StringBuilder sb, LogCollector.Entry entry) {
        string timeCol = LogSourceColors.DimBbHex(ColTime, GameSourceDimAmount);
        string bodyCol = LogSourceColors.DimBbHex(ColInfo, GameSourceDimAmount);
        sb.Append($"[color={timeCol}]--:--:--[/color] ");
        sb.Append($"[color={bodyCol}]{EscapeBbCode(entry.Text)}[/color]\n");
    }

    private static void AppendLiveEntryBody(
        StringBuilder sb,
        LogCollector.Entry entry,
        string source,
        string levelCol,
        HashSet<string> loadedModIds,
        Dictionary<string, string> modIdAliases) {
        string badge = LevelBadge(entry.Level);
        sb.Append($"[color={levelCol}]{badge,-4}[/color]");

        if (source == "Game" ||
            !TryFindModTagSpan(entry.Text, loadedModIds, modIdAliases, out int tagStart, out int tagEnd, out _)) {
            sb.Append($" [color={levelCol}]{EscapeBbCode(entry.Text)}[/color]");
            return;
        }

        string modCol = LogSourceColors.ColorToBbHex(LogSourceColors.GetModHighlightColor(source));
        string prefix = entry.Text[..tagStart];
        string tag = entry.Text[tagStart..tagEnd];

        if (prefix.Length > 0)
            sb.Append($" [color={levelCol}]{EscapeBbCode(prefix)}[/color]");
        else
            sb.Append(' ');

        sb.Append($"[color={modCol}]{EscapeBbCode(tag)}[/color]");

        int pos = tagEnd;
        while (TryFindSecondaryTagSpan(
                   entry.Text,
                   pos,
                   source,
                   loadedModIds,
                   modIdAliases,
                   out int secondaryStart,
                   out int secondaryEnd,
                   out bool isContentModTag,
                   out var secondaryInner)) {
            if (secondaryStart > pos)
                sb.Append($"[color={levelCol}]{EscapeBbCode(entry.Text[pos..secondaryStart])}[/color]");
            else if (secondaryStart == pos && entry.Text[secondaryStart] == '[')
                sb.Append(' ');

            string secondaryCol = isContentModTag
                ? LogSourceColors.ColorToBbHex(LogSourceColors.GetModHighlightColor(secondaryInner))
                : ColTime;
            sb.Append($"[color={secondaryCol}]{EscapeBbCode(entry.Text[secondaryStart..secondaryEnd])}[/color]");
            pos = secondaryEnd;
        }

        if (pos < entry.Text.Length)
            sb.Append($"[color={levelCol}]{EscapeBbCode(entry.Text[pos..])}[/color]");
    }

    private static bool TryFindSecondaryTagSpan(
        string text,
        int searchFrom,
        string primaryModId,
        HashSet<string> loadedModIds,
        Dictionary<string, string> modIdAliases,
        out int tagStart,
        out int tagEndExclusive,
        out bool isContentModTag,
        out string tagInner) {
        tagStart = 0;
        tagEndExclusive = 0;
        isContentModTag = false;
        tagInner = "";

        int pos = searchFrom;
        while (pos < text.Length) {
            while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                pos++;

            if (pos >= text.Length || text[pos] != '[')
                return false;

            int open = pos;
            int close = text.IndexOf(']', open + 1);
            if (close <= open + 1) {
                pos = open + 1;
                continue;
            }

            tagInner = text.Substring(open + 1, close - open - 1);
            if (LogLevelBracketTags.Contains(tagInner)) {
                pos = close + 1;
                continue;
            }

            if (tagInner.Equals(primaryModId, StringComparison.OrdinalIgnoreCase)) {
                pos = close + 1;
                continue;
            }

            if (!LooksLikeModOrScopeTag(tagInner)) {
                pos = close + 1;
                continue;
            }

            tagStart = open;
            tagEndExclusive = close + 1;
            if (TryResolveModId(tagInner, loadedModIds, modIdAliases, out _))
                isContentModTag = true;

            return true;
        }

        return false;
    }

    static bool LooksLikeModOrScopeTag(string inner) {
        if (string.IsNullOrWhiteSpace(inner))
            return false;

        return inner.IndexOf('=') < 0 && inner.IndexOf(',') < 0;
    }

    private static void RefreshStatsPanel(
        VBoxContainer statsVBox,
        Dictionary<string, int> modStats) {
        foreach (Node child in statsVBox.GetChildren())
            child.QueueFree();

        if (modStats.Count == 0) {
            var empty = new Label { Text = I18N.T("log.stats.empty", "无可见日志") };
            empty.AddThemeFontSizeOverride("font_size", 11);
            empty.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            statsVBox.AddChild(empty);
            return;
        }

        // Sort: Game first, then mods by count desc
        var sorted = new List<KeyValuePair<string, int>>(modStats);
        sorted.Sort((a, b) => {
            if (a.Key == "Game") return -1;
            if (b.Key == "Game") return 1;
            return b.Value.CompareTo(a.Value);
        });

        foreach (var (source, count) in sorted) {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            Color sliceCol = LogSourceColors.GetSliceColor(source, 0);

            var nameLabel = new Label {
                Text = source,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ClipText = true,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 11);
            nameLabel.AddThemeColorOverride("font_color", sliceCol);
            nameLabel.TooltipText = source;
            row.AddChild(nameLabel);

            var countLabel = new Label { Text = count.ToString() };
            countLabel.AddThemeFontSizeOverride("font_size", 11);
            countLabel.AddThemeColorOverride("font_color", sliceCol);
            row.AddChild(countLabel);

            statsVBox.AddChild(row);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Short label for mod id chips in the log filter rail.</summary>
    private static string ShortModLabel(string modId) {
        const int MaxLen = 26;
        if (modId.Length <= MaxLen) return modId;
        return modId[..(MaxLen - 1)] + "…";
    }

    /// <summary>Produce a compact display label for a rule pattern.</summary>
    private static string ShortRuleLabel(string pattern) {
        // Take up to the first 22 chars, trim at last space before cutoff
        const int MaxLen = 22;
        if (pattern.Length <= MaxLen) return pattern;
        int cut = pattern.LastIndexOf(' ', MaxLen);
        return cut > 4 ? pattern[..cut] + "…" : pattern[..MaxLen] + "…";
    }

    private static string LevelBadge(LogLevel level) => level switch {
        LogLevel.Error => "ERR ",
        LogLevel.Warn => "WARN",
        LogLevel.Info => "INFO",
        LogLevel.Load => "LOAD",
        LogLevel.Debug => "DBG ",
        LogLevel.VeryDebug => "VDB ",
        _ => "?   "
    };

    private static string LevelColor(LogLevel level) => level switch {
        LogLevel.Error => ColError,
        LogLevel.Warn => ColWarn,
        LogLevel.Info => ColInfo,
        _ => ColDebug
    };

    /// <summary>Escape characters that would be interpreted as BBCode tags.</summary>
    private static string EscapeBbCode(string text)
        => text.Replace("[", "[lb]");

    private static string GetGameLogsDirectory() => GameLogFileHydrator.LogsDirectory;

    private static void OpenGameLogsFolder() {
        var dir = GetGameLogsDirectory();
        try {
            Directory.CreateDirectory(dir);
            OS.ShellOpen(dir);
        }
        catch (Exception ex) {
            KitLog.Warn("LogViewer", $"Open logs folder failed: {ex.Message}");
        }
    }

    // ── Header builder ────────────────────────────────────────────────────

    private static Button? _clearBtn;
    private static Label? _kitlogErrorLabel;

    private static Button BuildHeader(VBoxContainer vbox, Action onClose) {
        _kitlogErrorLabel = null;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var title = new Label { Text = I18N.T("log.title", "Log Viewer") };
        title.AddThemeFontSizeOverride("font_size", 13);
        title.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        title.AddThemeConstantOverride("margin_left", 4);
        row.AddChild(title);

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var exportBtn = new Button {
            Text = I18N.T("log.export", "Log Export"),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(64, 26),
            Icon = MdiIcon.ZipBox.Texture(14, KitLibTheme.Subtle),
            TooltipText = I18N.T("log.export.subtitle",
                "Export selected game log and diagnostics as a ZIP package."),
        };
        ApplySmallFlatButton(exportBtn);
        row.AddChild(exportBtn);

        var openFolderBtn = new Button {
            Text = I18N.T("log.openFolder", "Open Folder"),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(64, 26),
            Icon = MdiIcon.FolderOpen.Texture(14, KitLibTheme.Subtle),
            TooltipText = I18N.T("log.openFolderTip", "Open the game log folder (user://logs/) in the system file manager"),
        };
        ApplySmallFlatButton(openFolderBtn);
        openFolderBtn.Pressed += OpenGameLogsFolder;
        row.AddChild(openFolderBtn);

        var openKitlogBtn = new Button {
            Text = I18N.T("log.openKitlog", "Dev viewer"),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(64, 26),
            Icon = MdiIcon.Console.Texture(14, KitLibTheme.Subtle),
            TooltipText = I18N.T(
                "log.openKitlogTip",
                "Open the dev viewer in your browser; mirrors this viewer's filters live"),
        };
        ApplySmallFlatButton(openKitlogBtn);
        openKitlogBtn.Pressed += () => {
            if (_kitlogErrorLabel != null)
                _kitlogErrorLabel.Visible = false;
            if (!DevViewerLauncher.TryOpenLogs(out var error)) {
                if (_kitlogErrorLabel != null) {
                    _kitlogErrorLabel.Text = error
                        ?? I18N.T("devViewer.launchFailed", "Could not open the dev viewer.");
                    _kitlogErrorLabel.Visible = true;
                }
            }
        };
        row.AddChild(openKitlogBtn);

        _clearBtn = new Button {
            Text = I18N.T("log.clear", "Clear"),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(64, 26),
            Icon = MdiIcon.Delete.Texture(14, KitLibTheme.Subtle)
        };
        ApplySmallFlatButton(_clearBtn);
        row.AddChild(_clearBtn);

        var closeBtn = new Button {
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(28, 28),
            Icon = MdiIcon.Close.Texture(16, KitLibTheme.Subtle)
        };
        ApplySmallFlatButton(closeBtn);
        closeBtn.Pressed += onClose;
        row.AddChild(closeBtn);

        vbox.AddChild(row);

        var instanceRow = new Label { Text = KitLibInstance.LogViewerSubtitle };
        instanceRow.AddThemeFontSizeOverride("font_size", 10);
        instanceRow.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        instanceRow.AddThemeConstantOverride("margin_left", 4);
        vbox.AddChild(instanceRow);

        if (KitLibProcessScope.IsDualInstanceActive()) {
            var dualHint = new Label {
                Text = I18N.T("log.instance.dualHint",
                    "Dual-instance: logs share user://logs/godot.log. Open Dev viewer for live tail (this window PID {0}).",
                    KitLibInstance.ProcessId),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            dualHint.AddThemeFontSizeOverride("font_size", 10);
            dualHint.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            dualHint.AddThemeConstantOverride("margin_left", 4);
            vbox.AddChild(dualHint);
        }

        _kitlogErrorLabel = new Label {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _kitlogErrorLabel.AddThemeFontSizeOverride("font_size", 10);
        _kitlogErrorLabel.AddThemeColorOverride("font_color", KitLibTheme.RarityCurse);
        _kitlogErrorLabel.AddThemeConstantOverride("margin_left", 4);
        vbox.AddChild(_kitlogErrorLabel);

        vbox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.ButtonBgNormal,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });

        return exportBtn;
    }

    private static void WireLogExportExtension(DevPanelUI.DualColumnOverlayHandle dual, Button exportBtn) {
        var backBtn = BuildLogExportExtensionBackHeader(dual.ExtContent);
        backBtn.Pressed += () => dual.CloseExtension();

        var extScroll = new ScrollContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var exportInner = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        FeedbackReportUI.BuildContent(exportInner, compact: false);
        extScroll.AddChild(exportInner);
        dual.ExtContent.AddChild(extScroll);

        exportBtn.Pressed += () => dual.ToggleExtension();
    }

    private static void WireMainMenuLogExportExtension(
        Control overlayRoot,
        PanelContainer mainPanel,
        Button exportBtn,
        bool openInitially) {
        const string extName = "KitLibLogViewerLogExportExt";
        PanelContainer? extPanel = null;
        Tween? closeTween = null;

        void CloseExtension() {
            closeTween?.Kill();
            closeTween = null;
            if (extPanel == null || !GodotObject.IsInstanceValid(extPanel))
                return;

            float w = Mathf.Max(1f, extPanel.Size.X);
            closeTween = extPanel.CreateTween();
            closeTween.SetTrans(Tween.TransitionType.Cubic);
            closeTween.SetEase(Tween.EaseType.In);
            closeTween.TweenProperty(extPanel, "position:x", w, 0.28f);
            closeTween.TweenCallback(Callable.From(() => {
                closeTween = null;
                if (extPanel != null && GodotObject.IsInstanceValid(extPanel)) {
                    extPanel.QueueFree();
                    extPanel = null;
                }
            }));
        }

        void ToggleLogExportExtension() {
            if (extPanel != null && GodotObject.IsInstanceValid(extPanel)) {
                CloseExtension();
                return;
            }

            closeTween?.Kill();
            closeTween = null;

            extPanel = DevPanelUI.CreateMainMenuModalPanel(LogExportExtWidth);
            extPanel.Name = extName;
            extPanel.MouseFilter = Control.MouseFilterEnum.Stop;
            extPanel.AnchorTop = mainPanel.AnchorTop;
            extPanel.AnchorBottom = mainPanel.AnchorBottom;
            extPanel.OffsetTop = mainPanel.OffsetTop;
            extPanel.OffsetBottom = mainPanel.OffsetBottom;

            float mainHalfW = PanelW / 2f;
            float gap = 8f;
            extPanel.AnchorLeft = 0.5f;
            extPanel.AnchorRight = 0.5f;
            extPanel.OffsetLeft = mainHalfW + gap;
            extPanel.OffsetRight = mainHalfW + gap + LogExportExtWidth;

            var extContent = extPanel.GetNode<VBoxContainer>("Content");
            extContent.AddThemeConstantOverride("separation", 8);
            var backBtn = BuildLogExportExtensionBackHeader(extContent);
            backBtn.Pressed += CloseExtension;

            var extScroll = new ScrollContainer {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            var exportInner = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            FeedbackReportUI.BuildContent(exportInner, compact: false);
            extScroll.AddChild(exportInner);
            extContent.AddChild(extScroll);

            overlayRoot.AddChild(extPanel);
            DevPanelUI.PlayControlSlideOpenFromLeft(extPanel, 0.28f);
        }

        exportBtn.Pressed += ToggleLogExportExtension;
        if (openInitially)
            Callable.From(ToggleLogExportExtension).CallDeferred();
    }

    private static Button BuildLogExportExtensionBackHeader(VBoxContainer extVbox) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var backBtn = new Button {
            Text = I18N.T("room.ancients.back", "Back"),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 32),
        };
        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 6,
        };
        foreach (var st in new[] { "normal", "hover", "pressed", "focus" })
            backBtn.AddThemeStyleboxOverride(st, flat);
        backBtn.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        backBtn.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(backBtn);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        extVbox.AddChild(row);
        extVbox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.Separator,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });
        return backBtn;
    }

    private static void BuildHeaderClearWire(VBoxContainer _, Action onClear) {
        if (_clearBtn != null)
            _clearBtn.Pressed += onClear;
        _clearBtn = null;
    }

    private static void ApplySmallFlatButton(Button btn) {
        var normal = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 3,
            ContentMarginBottom = 3,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        var hover = new StyleBoxFlat {
            BgColor = KitLibTheme.ButtonBgNormal,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 3,
            ContentMarginBottom = 3,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", normal);
        btn.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        btn.AddThemeColorOverride("font_hover_color", KitLibTheme.TextPrimary);
        btn.AddThemeFontSizeOverride("font_size", 12);
    }
}
