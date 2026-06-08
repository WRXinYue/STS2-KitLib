using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KitLib.CombatStats;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>DevPanel overlay for per-combat damage statistics (MVP).</summary>
internal static partial class CombatStatsUI {
    private const string RootName = "KitLibCombatStats";
    private const float PanelW = 960f;
    private const int PieSplitInitialRight = 280;
    private const int PieSplitMinRight = 220;
    private const float BarAnimDuration = 0.22f;
    private const float ValueAnimDuration = 0.18f;

    private enum ViewMode {
        Summary,
        ByCard,
        BySource,
        ByTurn,
        Extended,
        Timeline,
        Run,
    }

    public static void Show(NGlobalUi globalUi) {
        Remove(globalUi);
        _panelOpen = true;

        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, PanelW, () => Remove(globalUi), contentSeparation: 10);

        var titleBox = new VBoxContainer();
        titleBox.AddThemeConstantOverride("separation", 4);
        titleBox.AddChild(DevPanelUI.CreatePanelTitle(I18N.T("combatStats.title", "Combat Stats")));
        var subtitle = new Label {
            Text = I18N.T("combatStats.subtitle",
                "Live combat statistics from CombatHistory. Updates during fights; last combat is kept after victory."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 11);
        subtitle.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        titleBox.AddChild(subtitle);
        vbox.AddChild(titleBox);
        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());

        var statusLabel = new Label { Text = "" };
        statusLabel.AddThemeFontSizeOverride("font_size", 11);
        statusLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        vbox.AddChild(statusLabel);

        var chipRow = new HBoxContainer();
        chipRow.AddThemeConstantOverride("separation", 6);

        var chipSummary = DevPanelUI.CreateFilterChip(I18N.T("combatStats.view.summary", "Summary"), active: true);
        var chipByCard = DevPanelUI.CreateFilterChip(I18N.T("combatStats.view.byCard", "By card"), active: false);
        var chipBySource = DevPanelUI.CreateFilterChip(I18N.T("combatStats.view.bySource", "Damage taken"), active: false);
        var chipByTurn = DevPanelUI.CreateFilterChip(I18N.T("combatStats.view.byTurn", "By turn"), active: false);
        var chipExtended = DevPanelUI.CreateFilterChip(I18N.T("combatStats.view.extended", "Extended"), active: false);
        var chipTimeline = DevPanelUI.CreateFilterChip(I18N.T("combatStats.view.timeline", "Timeline"), active: false);
        var chipRun = DevPanelUI.CreateFilterChip(I18N.T("combatStats.view.run", "Run total"), active: false);
        chipRow.AddChild(chipSummary);
        chipRow.AddChild(chipByTurn);
        chipRow.AddChild(chipByCard);
        chipRow.AddChild(chipBySource);
        chipRow.AddChild(chipExtended);
        chipRow.AddChild(chipTimeline);
        chipRow.AddChild(chipRun);

        var chipScroll = new ScrollContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 30),
        };
        chipScroll.AddChild(chipRow);
        vbox.AddChild(chipScroll);

        var playerRow = new HBoxContainer();
        playerRow.AddThemeConstantOverride("separation", 6);
        playerRow.Visible = false;
        vbox.AddChild(playerRow);

        var splitOptions = new DevPanelUI.SplitBodyOptions {
            Name = "stats.body.split",
            InitialSplitRight = PieSplitInitialRight,
            MinSplitRight = PieSplitMinRight,
            MinMainWidth = 320,
        };
        var body = DevPanelUI.CreateSplitBody(splitOptions);
        var scroll = body.MainScroll;
        var inner = body.MainInner;

        var panelPie = new CategoryPieSidebarPanel("stats.pie.sidebar");
        var pieScroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        pieScroll.AddChild(panelPie.Root);
        body.SidebarPanel.AddChild(pieScroll);

        var detachSplitInit = DevPanelUI.AttachSplitInit(body, splitOptions);
        var detachMergeStyle = DevPanelUI.AttachMergeStyle(body);
        vbox.AddChild(body.Split);

        var exportRow = new HBoxContainer();
        exportRow.AddThemeConstantOverride("separation", 8);
        var exportBtn = new Button {
            Text = I18N.T("combatStats.export", "Export JSON"),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        exportBtn.AddThemeFontSizeOverride("font_size", 11);
        var exportStatus = new Label { Text = "" };
        exportStatus.AddThemeFontSizeOverride("font_size", 10);
        exportStatus.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        exportStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        exportRow.AddChild(exportBtn);
        exportRow.AddChild(exportStatus);

        var mpOverlayToggle = new CheckButton {
            Text = I18N.T("combatStats.mpOverlay.enabled", "Show top-right score panel"),
            ButtonPressed = SettingsStore.Current.CombatStatsMpOverlayEnabled,
        };
        mpOverlayToggle.AddThemeFontSizeOverride("font_size", 11);
        mpOverlayToggle.Pressed += () => {
            SettingsStore.SetCombatStatsMpOverlayEnabled(mpOverlayToggle.ButtonPressed);
            SyncMultiplayerOverlayState(globalUi);
        };

        vbox.AddChild(mpOverlayToggle);
        vbox.AddChild(exportRow);

        ViewMode mode = ViewMode.Summary;
        string? contentFingerprint = null;
        string? selectedPlayerKey = null;
        var playerChips = new List<(string Key, Button Chip)>();

        bool displayPending = false;
        bool pendingForceRebuild = false;
        void ScheduleUpdateDisplay(bool forceRebuild, bool animate) {
            if (!GodotObject.IsInstanceValid(root))
                return;
            pendingForceRebuild |= forceRebuild;
            if (displayPending)
                return;
            displayPending = true;
            bool shouldAnimate = animate;
            Callable.From(() => {
                displayPending = false;
                if (!GodotObject.IsInstanceValid(root))
                    return;
                bool rebuild = pendingForceRebuild;
                pendingForceRebuild = false;
                UpdateDisplay(rebuild, shouldAnimate);
                DevPanelUI.NotifyBrowserContextLayoutChanged(globalUi);
                SyncMultiplayerOverlayState(globalUi);
                MonsterIntentOverlayUI.SyncState(globalUi);
            }).CallDeferred();
        }

        void SetMode(ViewMode next) {
            mode = next;
            contentFingerprint = null;
            chipSummary.SetPressedNoSignal(next == ViewMode.Summary);
            chipByCard.SetPressedNoSignal(next == ViewMode.ByCard);
            chipBySource.SetPressedNoSignal(next == ViewMode.BySource);
            chipByTurn.SetPressedNoSignal(next == ViewMode.ByTurn);
            chipExtended.SetPressedNoSignal(next == ViewMode.Extended);
            chipTimeline.SetPressedNoSignal(next == ViewMode.Timeline);
            chipRun.SetPressedNoSignal(next == ViewMode.Run);
            ScheduleUpdateDisplay(forceRebuild: true, animate: false);
        }

        chipSummary.Pressed += () => SetMode(ViewMode.Summary);
        chipByCard.Pressed += () => SetMode(ViewMode.ByCard);
        chipBySource.Pressed += () => SetMode(ViewMode.BySource);
        chipByTurn.Pressed += () => SetMode(ViewMode.ByTurn);
        chipExtended.Pressed += () => SetMode(ViewMode.Extended);
        chipTimeline.Pressed += () => SetMode(ViewMode.Timeline);
        chipRun.Pressed += () => SetMode(ViewMode.Run);

        exportBtn.Pressed += () => {
            try {
                var bundle = CombatStatsExport.CaptureBundle();
                string json = CombatStatsExport.ToJson(bundle);
                DisplayServer.ClipboardSet(json);

                string dir = System.IO.Path.Combine(OS.GetUserDataDir(), "mod_data", "KitLib");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, $"combat-stats-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                System.IO.File.WriteAllText(path, json);
                exportStatus.Text = I18N.T("combatStats.exported", "Copied + saved to {0}", path);
            }
            catch (Exception ex) {
                exportStatus.Text = I18N.T("combatStats.exportFailed", "Export failed: {0}", ex.Message);
            }
        };

        void RebuildPlayerChips(CombatStatsSnapshot? snap) {
            foreach (var (_, chip) in playerChips) {
                playerRow.RemoveChild(chip);
                chip.Free();
            }
            playerChips.Clear();
            playerRow.Visible = false;

            if (snap == null || snap.Players.Count <= 1 || mode == ViewMode.Run)
                return;

            playerRow.Visible = true;
            foreach (var (key, stats) in snap.Players.OrderBy(kv => kv.Value.DisplayName)) {
                var chip = DevPanelUI.CreateFilterChip(
                    string.IsNullOrWhiteSpace(stats.DisplayName) ? key : stats.DisplayName,
                    active: key == selectedPlayerKey);
                playerChips.Add((key, chip));
                playerRow.AddChild(chip);
                string captured = key;
                chip.Pressed += () => {
                    selectedPlayerKey = captured;
                    contentFingerprint = null;
                    foreach (var (k, c) in playerChips)
                        c.SetPressedNoSignal(k == captured);
                    ScheduleUpdateDisplay(forceRebuild: true, animate: false);
                };
            }
        }

        void RefreshPanelPie(PlayerCombatStats? player) {
            if (!GodotObject.IsInstanceValid(panelPie.Root))
                return;
            panelPie.SetContext(player);
            panelPie.PrepareForViewMode(mode);
            panelPie.Refresh();
        }

        void RefreshGameContextPane(CombatStatsSnapshot? snap, PlayerCombatStats? player, bool isRun) {
            RefreshPanelPie(player);
            SyncGameContextPane(mode, snap, player, isRun);
        }

        void UpdateDisplay(bool forceRebuild, bool animate) {
            if (!GodotObject.IsInstanceValid(root))
                return;
            if (mode == ViewMode.Run) {
                statusLabel.Text = I18N.T("combatStats.status.run",
                    "Run total — {0} combat(s)", CombatStatsTracker.RunCombatCount);
                playerRow.Visible = false;

                string runFingerprint = $"run|{CombatStatsTracker.RunCombatCount}|{CombatStatsTracker.RunTotal.PrimaryPlayer?.DamageDealt ?? 0}";
                if (!forceRebuild && contentFingerprint == runFingerprint && inner.GetChildCount() > 0) {
                    var runPlayer = CombatStatsTracker.RunTotal.PrimaryPlayer;
                    if (runPlayer != null) {
                        RefreshExtended(inner, runPlayer, CombatStatsTracker.RunTotal.MaxTurn, animate);
                        RefreshGameContextPane(CombatStatsTracker.RunTotal, runPlayer, isRun: true);
                    }
                    return;
                }

                ClearScrollContent(inner);
                contentFingerprint = runFingerprint;
                BuildRunView(inner);
                ResetScrollLayout(scroll);
                RefreshGameContextPane(CombatStatsTracker.RunTotal, CombatStatsTracker.RunTotal.PrimaryPlayer, isRun: true);
                return;
            }

            var snap = CombatStatsTracker.IsTracking
                ? CombatStatsTracker.Current
                : CombatStatsTracker.Last;

            if (forceRebuild || playerChips.Count == 0)
                RebuildPlayerChips(snap);

            if (snap == null || snap.Players.Count == 0) {
                if (forceRebuild || inner.GetChildCount() == 0) {
                    ClearScrollContent(inner);
                    contentFingerprint = null;
                }
                statusLabel.Text = I18N.T("combatStats.empty", "No combat data yet. Enter a fight to begin tracking.");
                if (inner.GetChildCount() == 0)
                    inner.AddChild(MakeHintLabel(I18N.T("combatStats.emptyHint",
                        "Tracking starts automatically when Dev Mode is active and a combat begins.")));
                ResetScrollLayout(scroll);
                RefreshGameContextPane(null, null, isRun: false);
                return;
            }

            string encounter = string.IsNullOrEmpty(snap.EncounterKey) ? "—" : snap.EncounterKey;
            statusLabel.Text = snap.IsActive
                ? I18N.T("combatStats.status.live", "Live — {0}", encounter)
                : I18N.T("combatStats.status.last", "Last combat — {0}", encounter);

            if (selectedPlayerKey == null || !snap.Players.ContainsKey(selectedPlayerKey))
                selectedPlayerKey = snap.Players.Keys.First();

            var player = snap.Players[selectedPlayerKey];
            foreach (var (k, c) in playerChips)
                c.SetPressedNoSignal(k == selectedPlayerKey);

            string fingerprint = BuildFingerprint(mode, player, snap.MaxTurn);
            bool canRefresh = !forceRebuild
                              && contentFingerprint == fingerprint
                              && inner.GetChildCount() > 0;

            if (canRefresh) {
                RefreshContent(inner, mode, player, snap.MaxTurn, animate);
                RefreshGameContextPane(snap, player, isRun: false);
                return;
            }

            ClearScrollContent(inner);
            contentFingerprint = fingerprint;
            scroll.ScrollVertical = 0;

            switch (mode) {
                case ViewMode.Summary:
                    BuildSummary(inner, player, snap.MaxTurn, animate: false);
                    if (CombatStatsTracker.IsTracking
                        && CombatStatsTracker.Last?.Players.TryGetValue(player.Key, out var lastPlayer) == true)
                        BuildCompareSection(inner, player, lastPlayer);
                    break;
                case ViewMode.ByCard:
                    BuildRankedList(inner, CombatScoreCalculator.CardContributionByKey(player),
                        I18N.T("combatStats.col.card", "Card"),
                        CombatScoreCalculator.TotalScore(player), animate: false);
                    break;
                case ViewMode.BySource:
                    BuildRankedList(inner, player.DamageTakenBySource,
                        I18N.T("combatStats.col.source", "Source"),
                        player.DamageTaken, animate: false);
                    break;
                case ViewMode.ByTurn:
                    BuildTurnList(inner, player.DamagePerTurn, snap.MaxTurn, animate: false);
                    break;
                case ViewMode.Extended:
                    BuildExtendedView(inner, player, snap.MaxTurn);
                    break;
                case ViewMode.Timeline:
                    BuildTimelineView(inner, player);
                    break;
            }

            ResetScrollLayout(scroll);
            RefreshGameContextPane(snap, player, isRun: false);
        }

        Action onStatsChanged = () => {
            if (!GodotObject.IsInstanceValid(root))
                return;
            ScheduleUpdateDisplay(forceRebuild: false, animate: false);
        };
        CombatStatsTracker.Changed += onStatsChanged;

        root.TreeExiting += () => {
            if (((Node)globalUi).GetNodeOrNull<Control>(RootName) != root)
                return;
            CombatStatsTracker.Changed -= onStatsChanged;
            _panelOpen = false;
            detachSplitInit();
            detachMergeStyle();
            DevPanelUI.ResetContextPaneToDefault();
        };

        ((Node)globalUi).AddChild(root);
        ScheduleUpdateDisplay(forceRebuild: true, animate: false);
        SyncMultiplayerOverlayState(globalUi);
        MonsterIntentOverlayUI.SyncState(globalUi);
    }

    private static void ClearScrollContent(VBoxContainer inner) {
        while (inner.GetChildCount() > 0) {
            var child = inner.GetChild(0);
            inner.RemoveChild(child);
            child.Free();
        }
    }

    private static void ResetScrollLayout(ScrollContainer scroll) {
        scroll.ScrollVertical = 0;
        Callable.From(() => {
            if (GodotObject.IsInstanceValid(scroll))
                scroll.ScrollVertical = 0;
        }).CallDeferred();
    }

    public static void Remove(NGlobalUi globalUi) {
        _panelOpen = false;
        DevPanelUI.ResetContextPaneToDefault();
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
        SyncMultiplayerOverlayState(globalUi);
        MonsterIntentOverlayUI.SyncState(globalUi);
    }

    /// <summary>Layout-only key — value changes refresh in place to avoid QueueFree churn during combat.</summary>
    private static string BuildFingerprint(ViewMode mode, PlayerCombatStats player, int maxTurn) {
        var sb = new StringBuilder(128);
        sb.Append((int)mode).Append('|').Append(player.Key).Append('|');
        switch (mode) {
            case ViewMode.Summary:
            case ViewMode.ByTurn:
                sb.Append(maxTurn);
                break;
            case ViewMode.ByCard:
                AppendKeys(sb, CombatScoreCalculator.CardContributionByKey(player).Keys.OrderBy(k => k));
                break;
            case ViewMode.BySource:
                AppendKeys(sb, player.DamageTakenBySource.Keys.OrderBy(k => k));
                break;
            case ViewMode.Extended:
                sb.Append(maxTurn).Append('|');
                sb.Append(player.PowerDamageBySource.Count).Append('|');
                sb.Append(player.BlockByCard.Count).Append('|');
                sb.Append(player.PotionUseCount.Count);
                break;
            case ViewMode.Timeline:
                break;
        }
        return sb.ToString();
    }

    private static void AppendKeys(StringBuilder sb, IEnumerable<string> keys) {
        foreach (var key in keys)
            sb.Append(key).Append('\u001f');
    }

    private static void RefreshContent(
        VBoxContainer inner,
        ViewMode mode,
        PlayerCombatStats player,
        int maxTurn,
        bool animate) {
        switch (mode) {
            case ViewMode.Summary:
                FindValueRow(inner, "stat.score")?.SetValue(CombatScoreCalculator.TotalScore(player), animate);
                FindValueRow(inner, "stat.dealt")?.SetValue(player.DamageDealt, animate);
                FindValueRow(inner, "stat.hits")?.SetValue(player.HitCount, animate);
                FindValueRow(inner, "stat.cards")?.SetValue(player.CardsPlayed, animate);
                FindValueRow(inner, "stat.turns")?.SetValue(maxTurn, animate);
                FindValueRow(inner, "stat.taken")?.SetValue(player.DamageTaken, animate);
                FindValueRow(inner, "stat.block")?.SetValue(player.BlockGained, animate);
                RefreshTurnChart(inner, player.DamagePerTurn, maxTurn, animate, turnLimit: 8);
                break;
            case ViewMode.ByCard:
                RefreshBarRows(inner,
                    TopEntries(CombatScoreCalculator.CardContributionByKey(player), 24),
                    CombatScoreCalculator.TotalScore(player), animate);
                break;
            case ViewMode.BySource:
                RefreshBarRows(inner, TopEntries(player.DamageTakenBySource, 24), player.DamageTaken, animate);
                break;
            case ViewMode.ByTurn:
                RefreshTurnChart(inner, player.DamagePerTurn, maxTurn, animate);
                break;
            case ViewMode.Extended:
                RefreshExtended(inner, player, maxTurn, animate);
                break;
            case ViewMode.Timeline:
                RefreshTimeline(inner, player, animate);
                break;
        }
    }

    private static StatValueRow? FindValueRow(Node root, string id) =>
        root.FindChild(id, recursive: true, owned: false) as StatValueRow;

    private static void RefreshBarRows(
        Node root,
        IEnumerable<(string Name, int Amount)> entries,
        int maxAmount,
        bool animate) {
        int max = Math.Max(maxAmount, 1);
        foreach (var (name, amount) in entries) {
            var row = root.FindChild(StatBarRow.NameForKey(name), recursive: true, owned: false) as StatBarRow;
            row?.SetData(name, amount, max, animate);
        }
    }

    private static void BuildSummary(VBoxContainer parent, PlayerCombatStats player, int maxTurn, bool animate) {
        parent.AddChild(MakeSectionCard(I18N.T("combatStats.section.score", "Combat score"), section => {
            section.AddChild(MakeValueRow("stat.score", I18N.T("combatStats.score", "Total"),
                CombatScoreCalculator.TotalScore(player), animate));
        }));

        parent.AddChild(MakeSectionCard(I18N.T("combatStats.section.offense", "Offense"), section => {
            section.AddChild(MakeValueRow("stat.dealt", I18N.T("combatStats.dealt", "Damage dealt"), player.DamageDealt, animate));
            section.AddChild(MakeValueRow("stat.hits", I18N.T("combatStats.hits", "Hit count"), player.HitCount, animate));
            section.AddChild(MakeValueRow("stat.cards", I18N.T("combatStats.cardsPlayed", "Cards played"), player.CardsPlayed, animate));
            section.AddChild(MakeValueRow("stat.turns", I18N.T("combatStats.turns", "Turns recorded"), maxTurn, animate));
        }));

        parent.AddChild(MakeSectionCard(I18N.T("combatStats.section.defense", "Defense"), section => {
            section.AddChild(MakeValueRow("stat.taken", I18N.T("combatStats.taken", "Damage taken"), player.DamageTaken, animate));
            section.AddChild(MakeValueRow("stat.block", I18N.T("combatStats.block", "Block gained"), player.BlockGained, animate));
        }));

        if (player.DamagePerTurn.Count > 0) {
            parent.AddChild(MakeSectionCard(I18N.T("combatStats.section.topTurns", "Damage per turn"), section => {
                section.AddChild(MakeTurnDamageChart(player.DamagePerTurn, maxTurn, animate, turnLimit: 8));
            }));
        }
    }

    private static void BuildRankedList(
        VBoxContainer parent,
        Dictionary<string, int> data,
        string nameHeader,
        int totalForBars,
        bool animate) {
        if (data.Count == 0) {
            parent.AddChild(MakeHintLabel(I18N.T("combatStats.noData", "No entries yet.")));
            return;
        }

        parent.AddChild(MakeSectionCard(nameHeader, section => {
            foreach (var (name, amount) in TopEntries(data, 24))
                section.AddChild(MakeBarRow(name, amount, Math.Max(totalForBars, 1), animate));
        }));
    }

    private static void BuildTurnList(
        VBoxContainer parent,
        Dictionary<string, int> data,
        int maxTurn,
        bool animate) {
        if (data.Count == 0 && maxTurn <= 0) {
            parent.AddChild(MakeHintLabel(I18N.T("combatStats.noData", "No entries yet.")));
            return;
        }

        parent.AddChild(MakeTurnDamageChart(data, maxTurn, animate));
    }

    private static IEnumerable<(string Name, int Amount)> TopEntries(Dictionary<string, int> data, int limit) =>
        data.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(limit)
            .Select(kv => (kv.Key, kv.Value));

    private static Control MakeSectionCard(string title, Action<VBoxContainer> fillBody) =>
        DevPanelUI.MakeSectionCard(title, fillBody);

    private static StatValueRow MakeValueRow(string id, string label, int value, bool animate) {
        var row = new StatValueRow(label, value, animate) { Name = id };
        return row;
    }

    private static StatBarRow MakeBarRow(string name, int amount, int maxAmount, bool animate) {
        var row = new StatBarRow(name, amount, Math.Max(maxAmount, 1), animate) {
            Name = StatBarRow.NameForKey(name),
        };
        return row;
    }

    private static Label MakeHintLabel(string text) {
        var l = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        l.AddThemeFontSizeOverride("font_size", 11);
        l.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return l;
    }

    /// <summary>Label + animated integer value.</summary>
    private sealed partial class StatValueRow : HBoxContainer {
        private readonly Label _valueLabel;
        private int _displayed;
        private Tween? _tween;

        public StatValueRow(string label, int value, bool animate) {
            AddThemeConstantOverride("separation", 10);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var left = new Label {
                Text = label + ":",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            left.AddThemeFontSizeOverride("font_size", 11);
            left.AddThemeColorOverride("font_color", KitLibTheme.Subtle);

            _valueLabel = new Label {
                HorizontalAlignment = HorizontalAlignment.Right,
                CustomMinimumSize = new Vector2(52, 0),
            };
            _valueLabel.AddThemeFontSizeOverride("font_size", 11);
            _valueLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);

            AddChild(left);
            AddChild(_valueLabel);
            TreeExiting += () => _tween?.Kill();
            SetValue(value, animate);
        }

        public void SetValue(int value, bool animate) {
            if (!GodotObject.IsInstanceValid(_valueLabel))
                return;
            if (!animate || _displayed == value) {
                _tween?.Kill();
                _displayed = value;
                _valueLabel.Text = value.ToString();
                return;
            }

            int start = _displayed;
            _tween?.Kill();
            _tween = CreateTween();
            _tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.TweenMethod(Callable.From((float t) => {
                if (!GodotObject.IsInstanceValid(_valueLabel))
                    return;
                int v = (int)Math.Round(start + (value - start) * t);
                _displayed = v;
                _valueLabel.Text = v.ToString();
            }), 0f, 1f, ValueAnimDuration);
            _tween.Finished += () => {
                if (!GodotObject.IsInstanceValid(_valueLabel))
                    return;
                _displayed = value;
                _valueLabel.Text = value.ToString();
            };
        }
    }

    /// <summary>One ranked stat row: text line above, bar below (never overlaps).</summary>
    private sealed partial class StatBarRow : VBoxContainer {
        private readonly Label _nameLabel;
        private readonly Label _amountLabel;
        private readonly StatFractionBar _bar;

        public StatBarRow(string name, int amount, int maxAmount, bool animate) {
            AddThemeConstantOverride("separation", 4);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            ClipContents = true;

            var top = new HBoxContainer {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            top.AddThemeConstantOverride("separation", 8);

            _nameLabel = new Label {
                Text = CombatStatsDisplayNames.LocalizeKey(name),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 11);
            _nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);

            _amountLabel = new Label {
                Text = amount.ToString(),
                HorizontalAlignment = HorizontalAlignment.Right,
                CustomMinimumSize = new Vector2(52, 0),
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            };
            _amountLabel.AddThemeFontSizeOverride("font_size", 11);
            _amountLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);

            top.AddChild(_nameLabel);
            top.AddChild(_amountLabel);
            AddChild(top);

            float frac = maxAmount > 0 ? Math.Clamp((float)amount / maxAmount, 0f, 1f) : 0f;
            _bar = new StatFractionBar(animate ? 0f : frac);
            AddChild(_bar);
            TreeExiting += () => {
                if (GodotObject.IsInstanceValid(_amountLabel) && _amountLabel.HasMeta("_dm_amount_tween")) {
                    var existing = _amountLabel.GetMeta("_dm_amount_tween").AsGodotObject();
                    if (existing is Tween old)
                        old.Kill();
                }
            };
            _bar.SetFraction(frac, animate);
        }

        public static string NameForKey(string key) =>
            "bar." + key.Replace("/", "_").Replace(".", "_");

        public void SetData(string name, int amount, int maxAmount, bool animate) {
            if (!GodotObject.IsInstanceValid(_nameLabel))
                return;
            _nameLabel.Text = CombatStatsDisplayNames.LocalizeKey(name);
            AnimateAmount(_amountLabel, amount, animate);
            _bar.SetFraction(maxAmount > 0 ? Math.Clamp((float)amount / maxAmount, 0f, 1f) : 0f, animate);
        }

        private static void AnimateAmount(Label label, int target, bool animate) {
            if (!GodotObject.IsInstanceValid(label))
                return;
            if (!int.TryParse(label.Text, out int start))
                start = target;
            if (!animate || start == target) {
                label.Text = target.ToString();
                return;
            }

            if (label.HasMeta("_dm_amount_tween")) {
                var existing = label.GetMeta("_dm_amount_tween").AsGodotObject();
                if (existing is Tween old)
                    old.Kill();
            }

            var tween = label.CreateTween();
            label.SetMeta("_dm_amount_tween", tween);
            tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenMethod(Callable.From((float t) => {
                if (GodotObject.IsInstanceValid(label))
                    label.Text = ((int)Math.Round(start + (target - start) * t)).ToString();
            }), 0f, 1f, ValueAnimDuration);
            tween.Finished += () => {
                if (GodotObject.IsInstanceValid(label))
                    label.Text = target.ToString();
            };
        }
    }

    /// <summary>Track + fill using ColorRect (layout-safe, animatable width).</summary>
    private sealed partial class StatFractionBar : Control {
        private readonly ColorRect _fill;
        private float _displayFrac;
        private Tween? _tween;

        public StatFractionBar(float initialFraction) {
            _displayFrac = initialFraction;
            ClipContents = true;
            CustomMinimumSize = new Vector2(0, 8);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var track = new ColorRect {
                Color = new Color(KitLibTheme.ButtonBgNormal.R, KitLibTheme.ButtonBgNormal.G,
                    KitLibTheme.ButtonBgNormal.B, 0.9f),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            track.SetAnchorsPreset(LayoutPreset.FullRect);

            _fill = new ColorRect {
                Color = KitLibTheme.Accent,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _fill.SetAnchorsPreset(LayoutPreset.TopLeft);
            _fill.AnchorBottom = 1f;

            AddChild(track);
            AddChild(_fill);
            TreeExiting += () => _tween?.Kill();
            ApplyFillWidth();
        }

        public void SetFraction(float fraction, bool animate) {
            fraction = Mathf.Clamp(fraction, 0f, 1f);
            if (!animate || Mathf.IsEqualApprox(_displayFrac, fraction)) {
                _tween?.Kill();
                _displayFrac = fraction;
                ApplyFillWidth();
                return;
            }

            float start = _displayFrac;
            _tween?.Kill();
            _tween = CreateTween();
            _tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.TweenMethod(Callable.From((float t) => {
                _displayFrac = Mathf.Lerp(start, fraction, t);
                ApplyFillWidth();
            }), 0f, 1f, BarAnimDuration);
            _tween.Finished += () => {
                _displayFrac = fraction;
                ApplyFillWidth();
            };
        }

        private void ApplyFillWidth() {
            _fill.AnchorRight = _displayFrac;
            _fill.OffsetRight = 0f;
        }
    }
}
