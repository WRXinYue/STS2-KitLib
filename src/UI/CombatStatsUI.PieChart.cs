using System;
using System.Collections.Generic;
using System.Linq;
using DevMode.CombatStats;
using Godot;

namespace DevMode.UI;

internal static partial class CombatStatsUI {
    private const int PieChartSize = 168;

    private static CardPieSidebar MakePieSidebar() => new("stats.pie.sidebar");

    private static List<(string Name, int Amount, Color Color)> BuildPieSlices(
        Dictionary<string, int> data,
        int totalForPct,
        int limit,
        bool showAllEntries = false) {
        var entries = showAllEntries
            ? data.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key)
                .Select(kv => (kv.Key, kv.Value)).ToList()
            : TopEntries(data, limit).ToList();
        var slices = new List<(string Name, int Amount, Color Color)>(entries.Count + 1);
        int topSum = 0;
        for (int i = 0; i < entries.Count; i++) {
            var (name, amount) = entries[i];
            topSum += amount;
            slices.Add((CombatStatsDisplayNames.LocalizeKey(name), amount, PieSliceColor(i)));
        }

        if (showAllEntries)
            return slices;

        int other = Math.Max(totalForPct, topSum) - topSum;
        if (other > 0 && entries.Count > 0)
            slices.Add((I18N.T("combatStats.pie.other", "Other"), other, PieSliceColor(entries.Count)));

        return slices;
    }

    private static Dictionary<string, int> LocalizeOverviewData(Dictionary<string, int> data) {
        var localized = new Dictionary<string, int>(data.Count);
        foreach (var (kind, amount) in data) {
            string label = kind switch {
                "Damage" => I18N.T("combatStats.score.damage", "Damage"),
                "Block" => I18N.T("combatStats.score.block", "Block"),
                "Debuff" => I18N.T("combatStats.score.debuff", "Debuff"),
                "Buff" => I18N.T("combatStats.score.buff", "Buff"),
                "Utility" => I18N.T("combatStats.score.utility", "Utility cards"),
                "Potion" => I18N.T("combatStats.score.potion", "Potions"),
                "Synergy" => I18N.T("combatStats.score.synergy", "Debuff synergy"),
                _ => kind,
            };
            localized[label] = amount;
        }
        return localized;
    }

    private static Color PieSliceColor(int index) {
        float hue = (DevModeTheme.Accent.H + index * 0.14f) % 1f;
        return Color.FromHsv(hue, 0.58f, 0.92f);
    }

    /// <summary>Right pane: category chips + pie + legend (no separate panel chrome).</summary>
    private sealed partial class CardPieSidebar : VBoxContainer {
        private CombatPieCategory _category = CombatPieCategory.Overview;
        private readonly CombatStatsPieChart _chart;
        private readonly VBoxContainer _legend;
        private readonly Label _emptyLabel;
        private readonly Label _categoryHint;
        private readonly Dictionary<CombatPieCategory, Button> _chips = new();

        public CardPieSidebar(string name) {
            Name = name;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            CustomMinimumSize = new Vector2(PieSplitMinRight, 0);
            AddThemeConstantOverride("separation", 8);

            var title = new Label {
                Text = I18N.T("combatStats.pie.title", "Breakdown"),
            };
            title.AddThemeFontSizeOverride("font_size", 12);
            title.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
            AddChild(title);

            AddChild(BuildCategoryGrid());

            _categoryHint = new Label {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            _categoryHint.AddThemeFontSizeOverride("font_size", 10);
            _categoryHint.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
            AddChild(_categoryHint);

            _chart = new CombatStatsPieChart("stats.pie.chart");
            AddChild(_chart);

            _legend = new VBoxContainer {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _legend.AddThemeConstantOverride("separation", 4);
            AddChild(_legend);

            _emptyLabel = new Label {
                Text = I18N.T("combatStats.noData", "No entries yet."),
                Visible = false,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            _emptyLabel.AddThemeFontSizeOverride("font_size", 10);
            _emptyLabel.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
            AddChild(_emptyLabel);

            SetCategory(CombatPieCategory.Overview);
        }

        private Control BuildCategoryGrid() {
            var grid = new GridContainer {
                Columns = 2,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 6);

            AddCategoryChip(grid, CombatPieCategory.Overview,
                I18N.T("combatStats.pie.overview", "Overview"));
            AddCategoryChip(grid, CombatPieCategory.Cards,
                I18N.T("combatStats.pie.cards", "Cards"));
            AddCategoryChip(grid, CombatPieCategory.Offense,
                I18N.T("combatStats.pie.offense", "Offense"));
            AddCategoryChip(grid, CombatPieCategory.Support,
                I18N.T("combatStats.pie.support", "Support"));
            AddCategoryChip(grid, CombatPieCategory.Tank,
                I18N.T("combatStats.pie.tank", "Taken"));

            return grid;
        }

        private void AddCategoryChip(GridContainer grid, CombatPieCategory category, string label) {
            var chip = DevPanelUI.CreateFilterChip(label, active: category == _category);
            chip.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            grid.AddChild(chip);
            _chips[category] = chip;
            chip.Pressed += () => SetCategory(category);
        }

        private PlayerCombatStats? _lastPlayer;
        private string? _lastPieFingerprint;

        public void Refresh(PlayerCombatStats? player) {
            _lastPlayer = player;
            if (player == null) {
                _chart.Visible = false;
                _legend.Visible = false;
                _emptyLabel.Visible = true;
                _emptyLabel.Text = I18N.T("combatStats.noData", "No entries yet.");
                _lastPieFingerprint = null;
                return;
            }

            var (data, total) = CombatScoreCalculator.GetPieCategoryData(player, _category);
            if (_category == CombatPieCategory.Overview)
                data = LocalizeOverviewData(data);

            bool hasData = data.Count > 0 && total > 0;
            _chart.Visible = hasData;
            _legend.Visible = hasData;
            _emptyLabel.Visible = !hasData;

            if (!hasData) {
                _chart.SetSlices(Array.Empty<(string, int, Color)>(), 1);
                ClearLegend();
                _emptyLabel.Text = I18N.T("combatStats.noData", "No entries yet.");
                _lastPieFingerprint = null;
                return;
            }

            bool showAll = _category == CombatPieCategory.Overview;
            var slices = BuildPieSlices(data, total, limit: 5, showAllEntries: showAll);
            string fingerprint = BuildPieFingerprint(slices, total);
            if (fingerprint == _lastPieFingerprint)
                return;

            _lastPieFingerprint = fingerprint;
            _chart.SetSlices(slices, total);
            UpdateLegend(slices, total);
        }

        private void SetCategory(CombatPieCategory category) {
            _category = category;
            _lastPieFingerprint = null;
            foreach (var (cat, chip) in _chips)
                chip.SetPressedNoSignal(cat == category);
            _categoryHint.Text = CategoryHint(category);
            Refresh(_lastPlayer);
        }

        private void ClearLegend() {
            while (_legend.GetChildCount() > 0) {
                var child = _legend.GetChild(0);
                _legend.RemoveChild(child);
                child.Free();
            }
        }

        private static string BuildPieFingerprint(
            IReadOnlyList<(string Name, int Amount, Color Color)> slices,
            int total) {
            var sb = new System.Text.StringBuilder(64);
            sb.Append(total).Append('|');
            foreach (var (name, amount, _) in slices)
                sb.Append(name).Append(':').Append(amount).Append('\u001f');
            return sb.ToString();
        }

        private void UpdateLegend(IReadOnlyList<(string Name, int Amount, Color Color)> slices, int total) {
            while (_legend.GetChildCount() > slices.Count) {
                var extra = _legend.GetChild(_legend.GetChildCount() - 1);
                _legend.RemoveChild(extra);
                extra.Free();
            }

            for (int i = 0; i < slices.Count; i++) {
                var (name, amount, color) = slices[i];
                float pct = total > 0 ? 100f * amount / total : 0f;
                if (i < _legend.GetChildCount()) {
                    UpdateLegendRow((HBoxContainer)_legend.GetChild(i), name, amount, pct, color);
                }
                else {
                    _legend.AddChild(MakeLegendRow(name, amount, pct, color));
                }
            }
        }

        private static void UpdateLegendRow(HBoxContainer row, string name, int amount, float pct, Color color) {
            var swatch = (ColorRect)row.GetChild(0);
            var label = (Label)row.GetChild(1);
            var value = (Label)row.GetChild(2);
            swatch.Color = color;
            label.Text = name;
            value.Text = $"{amount} ({pct:0.#}%)";
        }

        private static Control MakeLegendRow(string name, int amount, float pct, Color color) {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var swatch = new ColorRect {
                Color = color,
                CustomMinimumSize = new Vector2(8, 8),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };

            var label = new Label {
                Text = name,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            };
            label.AddThemeFontSizeOverride("font_size", 10);
            label.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);

            var value = new Label {
                Text = $"{amount} ({pct:0.#}%)",
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            value.AddThemeFontSizeOverride("font_size", 10);
            value.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);

            row.AddChild(swatch);
            row.AddChild(label);
            row.AddChild(value);
            return row;
        }

        private static string CategoryHint(CombatPieCategory category) => category switch {
            CombatPieCategory.Overview => I18N.T("combatStats.pie.hint.overview",
                "Combat score by category: damage, block, debuffs, utility, etc."),
            CombatPieCategory.Cards => I18N.T("combatStats.pie.hint.cards",
                "Top cards by attributed contribution score."),
            CombatPieCategory.Offense => I18N.T("combatStats.pie.hint.offense",
                "Damage by card and power."),
            CombatPieCategory.Support => I18N.T("combatStats.pie.hint.support",
                "Block, utility, debuffs, buffs, potions, and synergy."),
            CombatPieCategory.Tank => I18N.T("combatStats.pie.hint.tank",
                "Damage taken by source."),
            _ => "",
        };
    }

    /// <summary>Donut pie chart (same draw approach as HarmonyOwnerPieChart).</summary>
    private sealed partial class CombatStatsPieChart : Control {
        private readonly List<(string Name, int Amount, Color Color)> _slices = new();
        private int _total = 1;

        public CombatStatsPieChart(string name) {
            Name = name;
            CustomMinimumSize = new Vector2(PieChartSize, PieChartSize);
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Ready() => Resized += () => QueueRedraw();

        public void SetSlices(IReadOnlyList<(string Name, int Amount, Color Color)> slices, int total) {
            _slices.Clear();
            _slices.AddRange(slices);
            _total = Math.Max(total, 1);
            QueueRedraw();
        }

        public override void _ExitTree() {
            _slices.Clear();
            base._ExitTree();
        }

        public override void _Draw() {
            var size = Size;
            var center = size / 2f;
            float outerR = Mathf.Min(size.X, size.Y) * 0.42f;
            if (outerR < 4f)
                return;

            if (_slices.Count == 0 || _total <= 0) {
                DrawCircle(center, outerR * 0.38f, new Color(0.22f, 0.22f, 0.26f, 0.85f));
                DrawArc(center, outerR, 0f, Mathf.Tau, 48, DevModeTheme.Separator, 1f, true);
                return;
            }

            float start = -Mathf.Pi / 2f;
            foreach (var (_, amount, color) in _slices) {
                float sweep = amount / (float)_total * Mathf.Tau;
                DrawWedge(center, outerR, start, start + sweep, color);
                start += sweep;
            }

            float innerR = outerR * 0.52f;
            DrawCircle(center, innerR, DevModeTheme.PanelBg);
            DrawArc(center, outerR + 0.5f, 0f, Mathf.Tau, 64, DevModeTheme.PanelBorder, 1f, true);
        }

        private void DrawWedge(Vector2 center, float radius, float fromRad, float toRad, Color color) {
            const int segments = 40;
            var pts = new Vector2[segments + 2];
            pts[0] = center;
            for (int i = 0; i <= segments; i++) {
                float t = i / (float)segments;
                float ang = Mathf.Lerp(fromRad, toRad, t);
                pts[i + 1] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
            }

            DrawColoredPolygon(pts, color);
        }
    }
}
