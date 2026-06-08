using System;
using System.Collections.Generic;
using System.Text;
using KitLib.Interop;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>
/// Full Harmony dump + smart analysis with selectable text, owner pie chart, and copy actions.
/// </summary>
internal static class HarmonyAnalysisUI {
    internal const string RootName = "KitLibHarmonyAnalysis";
    private const float PanelW = 1120f;
    private const double AutoRefreshIntervalSec = 3.0;
    private const int SmartMainSplitInitial = 820;
    private const int TypeListSplitInitial = 320;
    private const int TypeListMinWidth = 220;

    public static void Show(NGlobalUi globalUi) {
        Remove(globalUi);

        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, PanelW, () => Remove(globalUi), contentSeparation: 8);

        var titleBox = new VBoxContainer();
        titleBox.AddThemeConstantOverride("separation", 4);
        titleBox.AddChild(DevPanelUI.CreatePanelTitle(I18N.T("harmony.title", "Harmony patch analysis")));
        var subtitle = new Label {
            Text = I18N.T("harmony.subtitle",
                "Full dump matches typical framework patch exports. Smart analysis: risks + tables; owner share on the right."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        subtitle.AddThemeFontSizeOverride("font_size", 11);
        subtitle.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        titleBox.AddChild(subtitle);
        vbox.AddChild(titleBox);

        var selectHint = new Label {
            Text = I18N.T("harmony.selectHint",
                "Drag to select text in the report — Ctrl+C or “Copy selection”. Right-click for context menu."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        selectHint.AddThemeFontSizeOverride("font_size", 10);
        selectHint.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        vbox.AddChild(selectHint);
        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());

        // ── Exclude-owner filter bar ───────────────────────────────
        var filterRow = new HBoxContainer();
        filterRow.AddThemeConstantOverride("separation", 6);

        var filterLabel = new Label {
            Text = I18N.T("harmony.filter.label", "Exclude owners:"),
            VerticalAlignment = VerticalAlignment.Center
        };
        filterLabel.AddThemeFontSizeOverride("font_size", 11);
        filterLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        filterRow.AddChild(filterLabel);

        var excludeEdit = new LineEdit {
            Text = string.Join(", ", HarmonySmartAnalysis.DefaultExcludedOwners),
            PlaceholderText = I18N.T("harmony.filter.placeholder", "Comma-separated owner ids…"),
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 26)
        };
        excludeEdit.AddThemeFontSizeOverride("font_size", 11);
        filterRow.AddChild(excludeEdit);

        var filterHint = new Label {
            Text = I18N.T("harmony.filter.hint", "(Enter to apply)"),
            VerticalAlignment = VerticalAlignment.Center
        };
        filterHint.AddThemeFontSizeOverride("font_size", 10);
        filterHint.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        filterRow.AddChild(filterHint);

        vbox.AddChild(filterRow);

        var errLbl = new Label {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        errLbl.AddThemeFontSizeOverride("font_size", 11);
        errLbl.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.45f));
        vbox.AddChild(errLbl);

        var bodyDump = CreateReportTextEdit();

        var typeFilter = new LineEdit {
            PlaceholderText = I18N.T("harmony.smart.filterTypes", "Filter declaring types…"),
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28)
        };
        typeFilter.AddThemeFontSizeOverride("font_size", 11);

        var typeList = new HarmonyDeclaringTypeVirtualList {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(TypeListMinWidth, 200)
        };
        var typeScroll = new VScrollBar {
            CustomMinimumSize = new Vector2(16, 0)
        };
        typeList.BindScrollBar(typeScroll);

        var listRow = new HBoxContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        listRow.AddThemeConstantOverride("separation", 0);
        listRow.AddChild(typeList);
        listRow.AddChild(typeScroll);

        var listCol = new VBoxContainer {
            CustomMinimumSize = new Vector2(TypeListMinWidth + 20, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        listCol.AddThemeConstantOverride("separation", 6);
        listCol.AddChild(typeFilter);
        listCol.AddChild(listRow);

        var detailByType = CreateReportTextEdit();
        detailByType.CustomMinimumSize = new Vector2(320, 0);

        var typeBrowseSplit = new HSplitContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SplitOffset = TypeListSplitInitial
        };
        typeBrowseSplit.AddChild(listCol);
        typeBrowseSplit.AddChild(detailByType);

        var fullSmartReport = CreateReportTextEdit();

        var smartInnerTabs = new TabContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        smartInnerTabs.AddThemeConstantOverride("side_margin", 6);
        smartInnerTabs.AddChild(typeBrowseSplit);
        smartInnerTabs.AddChild(fullSmartReport);
        smartInnerTabs.SetTabTitle(0, I18N.T("harmony.smart.tab.byType", "By declaring type"));
        smartInnerTabs.SetTabTitle(1, I18N.T("harmony.smart.tab.fullReport", "Full report text"));

        var pieChart = new HarmonyOwnerPieChart();
        var legend = new Label {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        legend.AddThemeFontSizeOverride("font_size", 9);
        legend.AddThemeColorOverride("font_color", KitLibTheme.Subtle);

        var legendScroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 120)
        };
        legendScroll.AddChild(legend);

        var pieTitle = new Label {
            Text = I18N.T("harmony.pie.title", "Patch share by owner"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        pieTitle.AddThemeFontSizeOverride("font_size", 11);
        pieTitle.AddThemeColorOverride("font_color", KitLibTheme.Accent);

        var legendTitle = new Label { Text = I18N.T("harmony.pie.legendTitle", "Counts (all owners)") };
        legendTitle.AddThemeFontSizeOverride("font_size", 10);
        legendTitle.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);

        var rightColumn = new VBoxContainer {
            CustomMinimumSize = new Vector2(248, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        rightColumn.AddThemeConstantOverride("separation", 8);
        rightColumn.AddChild(pieTitle);
        rightColumn.AddChild(pieChart);
        rightColumn.AddChild(legendTitle);
        rightColumn.AddChild(legendScroll);

        var smartMainSplit = new HSplitContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SplitOffset = SmartMainSplitInitial
        };
        smartMainSplit.AddChild(smartInnerTabs);
        smartMainSplit.AddChild(rightColumn);

        var tabs = new TabContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        tabs.AddThemeConstantOverride("side_margin", 8);
        tabs.AddChild(bodyDump);
        tabs.AddChild(smartMainSplit);
        tabs.SetTabTitle(0, I18N.T("harmony.tab.full", "Full report"));
        tabs.SetTabTitle(1, I18N.T("harmony.tab.smart", "Smart analysis"));

        vbox.AddChild(tabs);

        const int tabDump = 0;
        const int smartByType = 0;

        TextEdit ActiveEditor() {
            if (tabs.CurrentTab == tabDump)
                return bodyDump;
            return smartInnerTabs.CurrentTab == smartByType ? detailByType : fullSmartReport;
        }

        HarmonyPatchRegistry lastRegistry = HarmonyPatchRegistry.Empty;
        IReadOnlyList<HarmonySmartAnalysis.DeclaringTypePatchInfo> lastByType =
            Array.Empty<HarmonySmartAnalysis.DeclaringTypePatchInfo>();

        void ApplyTypeSelection(HarmonySmartAnalysis.DeclaringTypePatchInfo? t, HarmonyPatchRegistry reg) {
            if (t == null) {
                detailByType.Text = I18N.T("harmony.smart.selectTypeHint", "Select a declaring type on the left to list mods and patches.");
                return;
            }

            detailByType.Text = FormatTypeDetail(t, reg);
        }

        typeList.ItemSelected += t => ApplyTypeSelection(t, lastRegistry);

        typeFilter.TextChanged += _ => {
            if (lastByType.Count == 0) return;
            typeList.SetData(lastByType, typeFilter.Text, resetSelection: true);
            ApplyTypeSelection(typeList.GetSelected(), lastRegistry);
        };

        HarmonySmartAnalysis.SmartAnalysisResult? lastAppliedSmart = null;
        string? lastSmartText = null;

        IReadOnlyList<string> ParseExcluded() {
            var result = new List<string>();
            foreach (var part in excludeEdit.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)) {
                var id = part.Trim();
                if (id.Length > 0) result.Add(id);
            }
            return result;
        }

        void Rebuild() {
            var parts = new List<string>();
            var patchRegistry = HarmonyPatchRegistry.Load(out var regErr);
            lastRegistry = patchRegistry;
            if (!string.IsNullOrEmpty(regErr))
                parts.Add(string.Format(I18N.T("harmony.registry.loadError", "Registry: {0}"), regErr));

            var fullText = HarmonyPatchReportBuilder.BuildReport(out var errDump);
            if (!string.IsNullOrEmpty(errDump)) {
                parts.Add(string.Format(I18N.T("harmony.error.dump", "Full report: {0}"), errDump));
                bodyDump.Text = "";
            }
            else {
                bodyDump.Text = fullText;
            }

            var smart = HarmonySmartAnalysis.Analyze(out var errSmart, ParseExcluded());
            if (!string.IsNullOrEmpty(errSmart)) {
                parts.Add(string.Format(I18N.T("harmony.error.smart", "Smart analysis: {0}"), errSmart));
                fullSmartReport.Text = "";
                detailByType.Text = "";
                pieChart.SetData(null);
                legend.Text = "";
                lastByType = Array.Empty<HarmonySmartAnalysis.DeclaringTypePatchInfo>();
                typeList.SetData(lastByType, typeFilter.Text, resetSelection: true);
            }
            else if (smart != null) {
                lastByType = smart.PatchesByDeclaringType;

                // FormatReport is expensive — skip if the analysis result hasn't changed.
                if (!ReferenceEquals(smart, lastAppliedSmart) || lastSmartText == null) {
                    lastSmartText = HarmonySmartAnalysis.FormatReport(
                        smart,
                        I18N.T("harmony.smart.heading", "=== Smart analysis (KitLib) ==="),
                        I18N.T("harmony.smart.section.risk", "Likely problem spots (heuristic)"),
                        I18N.T("harmony.smart.riskIntro",
                            "These patterns often correlate with mod conflicts or fragile ordering — not proof. Use owner + patch method names to find the mod DLL."),
                        I18N.T("harmony.smart.riskNone", "No transpiler stacks, same-priority multi-owner rows, or heavy prefix/postfix stacks matched the thresholds."),
                        I18N.T("harmony.smart.risk.transpiler",
                            "A) Multiple transpilers on one original method (high risk — IL rewrite order)"),
                        I18N.T("harmony.smart.risk.samePriority",
                            "B) Same hook kind + same priority + different owners (order vs other same-priority patches can be subtle)"),
                        string.Format(I18N.T("harmony.smart.risk.heavy",
                                "C) Many prefixes/postfixes on one method (threshold: ≥{0} / ≥{1})"),
                            HarmonySmartAnalysis.HeavyPrefixThreshold,
                            HarmonySmartAnalysis.HeavyPostfixThreshold),
                        I18N.T("harmony.smart.riskHintFooter",
                            "If something breaks, try disabling mods whose owner id appears above, or compare load order."),
                        I18N.T("harmony.smart.section.owners", "Patches by owner (Harmony id) — most active first"),
                        I18N.T("harmony.smart.section.busiest", "Busiest patched methods (most hooks)"),
                        I18N.T("harmony.smart.section.multi", "Same method, multiple distinct owners — review for interactions"),
                        I18N.T("harmony.smart.noneMulti", "No methods with 2+ distinct owners in this snapshot."),
                        I18N.T("harmony.smart.disclaimer",
                            "Heuristics only: multi-owner does not imply a bug; it flags overlap worth eyeballing."),
                        I18N.T("harmony.smart.col.hooks", "hooks"),
                        I18N.T("harmony.smart.col.px", "px"),
                        I18N.T("harmony.smart.col.po", "po"),
                        I18N.T("harmony.smart.col.tr", "tr"),
                        I18N.T("harmony.smart.col.fi", "fi"),
                        I18N.T("harmony.smart.col.owners", "owners"),
                        patchRegistry,
                        I18N.T("harmony.registry.section", "=== Patch documentation (shared registry) ==="),
                        I18N.T("harmony.registry.intro",
                            "Matched by Harmony owner id. Registry is embedded in KitLib.dll."),
                        I18N.T("harmony.registry.noneMatched",
                            "No owners in this snapshot appear in the registry."));
                    lastAppliedSmart = smart;
                }
                fullSmartReport.Text = lastSmartText;

                pieChart.SetData(smart.PatchesByOwner);
                legend.Text = BuildOwnerLegend(smart.PatchesByOwner, patchRegistry);

                typeList.SetData(lastByType, typeFilter.Text, resetSelection: true);
                ApplyTypeSelection(typeList.GetSelected(), patchRegistry);
            }
            else {
                fullSmartReport.Text = "";
                detailByType.Text = "";
                pieChart.SetData(null);
                legend.Text = "";
                lastByType = Array.Empty<HarmonySmartAnalysis.DeclaringTypePatchInfo>();
                typeList.SetData(lastByType, typeFilter.Text, resetSelection: true);
            }

            if (parts.Count > 0) {
                errLbl.Text = string.Join("  |  ", parts);
                errLbl.Visible = true;
            }
            else {
                errLbl.Visible = false;
            }
        }

        static string BuildOwnerLegend(IReadOnlyList<(string Owner, int PatchCount)> rows, HarmonyPatchRegistry registry) {
            if (rows.Count == 0) return "";
            var total = 0;
            foreach (var (_, n) in rows)
                total += n;
            if (total <= 0) return "";

            var sb = new StringBuilder();
            foreach (var (owner, n) in rows) {
                var pct = 100.0 * n / total;
                sb.Append(n.ToString().PadLeft(5)).Append("  ")
                    .Append(pct.ToString("0.#").PadLeft(5)).Append("%  ");
                if (registry.Count > 0 && registry.TryGet(owner, out var doc) && !string.IsNullOrEmpty(doc.Category))
                    sb.Append(owner).Append(" [").Append(doc.Category).Append(']');
                else
                    sb.Append(owner);
                sb.Append('\n');
            }

            return sb.ToString().TrimEnd();
        }

        static string FormatTypeDetail(HarmonySmartAnalysis.DeclaringTypePatchInfo t, HarmonyPatchRegistry reg) {
            var sb = new StringBuilder();
            sb.AppendLine($"[{t.DeclaringTypeFullName}]");
            sb.AppendLine(string.Format(I18N.T("harmony.smart.typeStats",
                    "Patched methods: {0}  |  patch ops: {1}  |  distinct owners: {2}"),
                t.Methods.Count,
                t.TotalPatchOperations,
                t.DistinctOwnerCount));
            sb.AppendLine(new string('─', 56));
            foreach (var m in t.Methods) {
                sb.AppendLine(m.MethodSignature);
                foreach (var line in m.Lines) {
                    var label = line.Owner;
                    var cat = "";
                    if (reg.TryGet(line.Owner, out var doc)) {
                        if (!string.IsNullOrEmpty(doc.DisplayName))
                            label = doc.DisplayName;
                        if (!string.IsNullOrEmpty(doc.Category))
                            cat = $" [{doc.Category}]";
                    }

                    sb.AppendLine(
                        $"  {line.HookKind,-11}  {label}{cat}  pri={line.Priority}  {line.PatchMethodRef}");
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        var appliedExcludeText = excludeEdit.Text;
        excludeEdit.TextSubmitted += _ => {
            appliedExcludeText = excludeEdit.Text;
            HarmonySmartAnalysis.InvalidateCache();
            Rebuild();
        };
        excludeEdit.FocusExited += () => {
            // Only rebuild if the text actually changed; avoids spurious rebuilds
            // every time focus moves to another control in the same panel.
            if (excludeEdit.Text == appliedExcludeText) return;
            appliedExcludeText = excludeEdit.Text;
            HarmonySmartAnalysis.InvalidateCache();
            Rebuild();
        };

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 8);
        var autoRefresh = new CheckButton {
            Text = I18N.T("bridge.autoRefresh", "Auto-refresh"),
            ButtonPressed = false,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };
        autoRefresh.AddThemeFontSizeOverride("font_size", 11);
        btnRow.AddChild(autoRefresh);

        var refresh = new Button {
            Text = I18N.T("bridge.refresh", "Refresh"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        refresh.Pressed += () => {
            HarmonyPatchRegistry.InvalidateCache();
            HarmonyPatchReportBuilder.InvalidateCache();
            HarmonySmartAnalysis.InvalidateCache();
            Rebuild();
        };
        btnRow.AddChild(refresh);

        var copySelBtn = new Button {
            Text = I18N.T("harmony.copySelection", "Copy selection"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        copySelBtn.Pressed += () => {
            var te = ActiveEditor();
            var s = te.GetSelectedText();
            if (!string.IsNullOrEmpty(s))
                DisplayServer.ClipboardSet(s);
        };
        btnRow.AddChild(copySelBtn);

        var copyAllBtn = new Button {
            Text = I18N.T("harmony.copyAllTab", "Copy all (tab)"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        copyAllBtn.Pressed += () => {
            var t = ActiveEditor().Text;
            if (!string.IsNullOrEmpty(t))
                DisplayServer.ClipboardSet(t);
        };
        btnRow.AddChild(copyAllBtn);

        vbox.AddChild(btnRow);

        var timer = new Godot.Timer {
            WaitTime = AutoRefreshIntervalSec,
            OneShot = false,
            Autostart = true
        };
        timer.Timeout += () => {
            if (autoRefresh.ButtonPressed)
                Rebuild();
        };
        root.AddChild(timer);

        Rebuild();

        ((Node)globalUi).AddChild(root);
    }

    private static TextEdit CreateReportTextEdit() {
        var te = new TextEdit {
            Editable = false,
            WrapMode = TextEdit.LineWrappingMode.None,
            ContextMenuEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            VirtualKeyboardEnabled = false,
            ScrollFitContentHeight = false
        };
        te.AddThemeFontSizeOverride("font_size", 11);
        te.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        te.AddThemeColorOverride("caret_color", KitLibTheme.Accent);
        te.AddThemeColorOverride("selection_color", new Color(KitLibTheme.Accent.R, KitLibTheme.Accent.G, KitLibTheme.Accent.B, 0.35f));

        var bg = new StyleBoxFlat {
            BgColor = new Color(KitLibTheme.PanelBg.R, KitLibTheme.PanelBg.G, KitLibTheme.PanelBg.B, 0.94f),
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 10,
            ContentMarginBottom = 12
        };
        te.AddThemeStyleboxOverride("normal", bg);
        te.AddThemeStyleboxOverride("read_only", bg);
        te.AddThemeStyleboxOverride("focus", bg);
        return te;
    }

    public static void Remove(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
    }
}
