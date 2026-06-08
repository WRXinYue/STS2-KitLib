using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.CombatStats;
using Godot;

namespace KitLib.UI;

internal static partial class CombatStatsUI {
    private const float SidebarBarHeight = 72f;
    private const float SidebarBarWidth = 14f;
    private const float RailCompactBarWidth = 10f;
    private const float RailCompactSingleBarMinHeight = 120f;

    private static bool SidebarUsesPie(ViewMode mode) => mode switch {
        ViewMode.ByCard or ViewMode.BySource or ViewMode.ByTurn or ViewMode.Timeline => true,
        _ => false,
    };

    private static CombatPieCategory PieCategoryForView(ViewMode mode) => mode switch {
        ViewMode.ByCard => CombatPieCategory.Cards,
        ViewMode.BySource => CombatPieCategory.Tank,
        ViewMode.ByTurn => CombatPieCategory.Overview,
        ViewMode.Timeline => CombatPieCategory.Overview,
        _ => CombatPieCategory.Overview,
    };

    private static IReadOnlyList<(string Key, int Amount, Color Color)> ScoreBreakdownSegments(
        CombatScoreBreakdown bd) {
        var segments = new List<(string, int, Color)>(7);
        AddSeg(segments, "Damage", bd.Damage, ScoreKindColor(0));
        AddSeg(segments, "Block", bd.Block, ScoreKindColor(1));
        AddSeg(segments, "Debuff", bd.Debuff, ScoreKindColor(2));
        AddSeg(segments, "Buff", bd.Buff, ScoreKindColor(3));
        AddSeg(segments, "Utility", bd.Utility, ScoreKindColor(4));
        AddSeg(segments, "Potion", bd.Potion, ScoreKindColor(5));
        AddSeg(segments, "Synergy", bd.Synergy, ScoreKindColor(6));
        return segments;
    }

    private static void AddSeg(
        List<(string Key, int Amount, Color Color)> list,
        string key,
        int amount,
        Color color) {
        if (amount > 0)
            list.Add((key, amount, color));
    }

    private static Color ScoreKindColor(int index) {
        float hue = (KitLibTheme.Accent.H + index * 0.12f) % 1f;
        return Color.FromHsv(hue, 0.58f, 0.92f);
    }

    private static string LocalizeScoreKind(string kind) => kind switch {
        "Damage" => I18N.T("combatStats.score.damage", "Damage"),
        "Block" => I18N.T("combatStats.score.block", "Block"),
        "Debuff" => I18N.T("combatStats.score.debuff", "Debuff"),
        "Buff" => I18N.T("combatStats.score.buff", "Buff"),
        "Utility" => I18N.T("combatStats.score.utility", "Utility cards"),
        "Potion" => I18N.T("combatStats.score.potion", "Potions"),
        "Synergy" => I18N.T("combatStats.score.synergy", "Debuff synergy"),
        _ => kind,
    };

    private static string ScoreKindAbbrev(string kind) => kind switch {
        "Damage" => "DMG",
        "Block" => "BLK",
        "Debuff" => "DBF",
        "Buff" => "BUF",
        "Utility" => "UTL",
        "Potion" => "POT",
        "Synergy" => "SYN",
        _ => kind.Length <= 3 ? kind.ToUpperInvariant() : kind[..3].ToUpperInvariant(),
    };

    private static void ApplyBarTooltip(Control control, string tooltip) {
        control.TooltipText = tooltip;
        control.MouseFilter = string.IsNullOrEmpty(tooltip)
            ? Control.MouseFilterEnum.Ignore
            : Control.MouseFilterEnum.Stop;
    }

    private static string ResolvePlayerDisplayName(PlayerCombatStats player) =>
        string.IsNullOrWhiteSpace(player.DisplayName) ? player.Key : player.DisplayName;

    private static string FormatPlayerTooltip(string name, int total, CombatScoreBreakdown bd) {
        var sb = new System.Text.StringBuilder(128);
        sb.Append(name).Append('\n');
        sb.Append(I18N.T("combatStats.sidebar.total", "Total {0}", total));

        AppendScoreLine(sb, "Damage", bd.Damage, total);
        AppendScoreLine(sb, "Block", bd.Block, total);
        AppendScoreLine(sb, "Debuff", bd.Debuff, total);
        AppendScoreLine(sb, "Buff", bd.Buff, total);
        AppendScoreLine(sb, "Utility", bd.Utility, total);
        AppendScoreLine(sb, "Potion", bd.Potion, total);
        AppendScoreLine(sb, "Synergy", bd.Synergy, total);

        return sb.ToString().TrimEnd();
    }

    private static void AppendScoreLine(System.Text.StringBuilder sb, string kind, int amount, int total) {
        if (amount <= 0)
            return;
        float pct = total > 0 ? 100f * amount / total : 0f;
        sb.Append('\n')
            .Append(LocalizeScoreKind(kind))
            .Append(' ')
            .Append(amount)
            .Append(" (")
            .Append(pct.ToString("0.#"))
            .Append("%)");
    }

    private static Control BuildScoreBreakdownTooltipControl(string name, int total, CombatScoreBreakdown bd) {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = KitLibTheme.PanelBg,
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        });

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);
        panel.AddChild(vbox);

        var title = new Label { Text = name };
        title.AddThemeFontSizeOverride("font_size", 11);
        title.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        vbox.AddChild(title);

        var totalLbl = new Label {
            Text = I18N.T("combatStats.sidebar.total", "Total {0}", total),
        };
        totalLbl.AddThemeFontSizeOverride("font_size", 10);
        totalLbl.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        vbox.AddChild(totalLbl);

        foreach (var (key, amount, color) in ScoreBreakdownSegments(bd)) {
            float pct = total > 0 ? 100f * amount / total : 0f;
            vbox.AddChild(MakeScoreBreakdownTooltipRow(key, amount, pct, color));
        }

        return panel;
    }

    private static Control MakeScoreBreakdownTooltipRow(string kind, int amount, float pct, Color color) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        var swatch = new Panel {
            CustomMinimumSize = new Vector2(10, 10),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        swatch.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = color,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        });
        row.AddChild(swatch);

        var line = new Label {
            Text = $"{LocalizeScoreKind(kind)} {amount} ({pct:0.#}%)",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        line.AddThemeFontSizeOverride("font_size", 10);
        line.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        row.AddChild(line);
        return row;
    }

    /// <summary>Default sidebar: per-player vertical score stacks.</summary>
    private sealed partial class PlayerContributionSidebarPanel : IDevPanelSidebarProvider {
        private readonly VBoxContainer _root;
        private readonly VBoxContainer _list;
        private readonly VBoxContainer _legend;
        private readonly Label _empty;
        private CombatStatsSnapshot? _snapshot;
        private bool _isRunView;
        private readonly bool _railCompact;
        private bool _hasContent;
        private string _lastStatsKey = "";

        public PlayerContributionSidebarPanel(bool railCompact = false) {
            _railCompact = railCompact;
            _root = new VBoxContainer {
                Name = "stats.sidebar.players",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            _root.AddThemeConstantOverride("separation", railCompact ? 4 : 10);

            _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _list.AddThemeConstantOverride("separation", railCompact ? 4 : 10);
            if (railCompact) {
                _list.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                _list.Alignment = BoxContainer.AlignmentMode.Center;
            }

            _legend = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _legend.AddThemeConstantOverride("separation", 4);
            _legend.Visible = !railCompact;

            _empty = new Label {
                Text = I18N.T("combatStats.noData", "No entries yet."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Visible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _empty.AddThemeFontSizeOverride("font_size", railCompact ? 8 : 10);
            _empty.AddThemeColorOverride("font_color", KitLibTheme.Subtle);

            _root.AddChild(_list);
            if (!railCompact)
                _root.AddChild(_legend);
            if (!railCompact)
                _root.AddChild(_empty);
            if (!railCompact)
                BuildLegend();
        }

        public Control Root => _root;
        public bool HasContent => _hasContent;
        public string Title => I18N.T("combatStats.sidebar.players", "Player scores");
        public string Hint => I18N.T("combatStats.sidebar.playersHint",
            "Bar height = total combat score. Colors = damage, block, setup, etc.");

        public void SetContext(CombatStatsSnapshot? snapshot, bool isRunView) {
            _snapshot = snapshot;
            _isRunView = isRunView;
        }

        public void PrepareForViewMode(ViewMode mode) { }

        public void Refresh() {
            var players = ResolvePlayers(_snapshot, _isRunView);
            string statsKey = BuildStatsKey(players);
            bool nextHasContent = players.Count > 0 && (!_railCompact || players.Count == 1);
            if (statsKey == _lastStatsKey && nextHasContent == _hasContent)
                return;

            _lastStatsKey = statsKey;
            ClearList();

            if (players.Count == 0) {
                _hasContent = false;
                if (!_railCompact) {
                    _empty.Visible = true;
                    _legend.Visible = false;
                }
                return;
            }

            _hasContent = true;
            if (!_railCompact)
                _empty.Visible = false;
            if (!_railCompact)
                _legend.Visible = true;
            int maxScore = Math.Max(1, players.Max(p => CombatScoreCalculator.TotalScore(p)));

            var ordered = players.OrderByDescending(CombatScoreCalculator.TotalScore)
                .ThenBy(p => p.DisplayName)
                .ToList();

            if (_railCompact) {
                if (ordered.Count != 1) {
                    _hasContent = false;
                    return;
                }

                int total = CombatScoreCalculator.TotalScore(ordered[0]);
                _list.AddChild(MakeSinglePlayerCompactColumn(ordered[0], total));
                return;
            }

            foreach (var player in ordered) {
                int total = CombatScoreCalculator.TotalScore(player);
                _list.AddChild(MakePlayerRow(player, total, maxScore));
            }
        }

        private string BuildStatsKey(List<PlayerCombatStats> players) {
            if (players.Count == 0)
                return "empty";
            if (_railCompact) {
                if (players.Count != 1)
                    return "compact:multi";
                return $"compact:{CombatScoreCalculator.TotalScore(players[0])}";
            }

            return string.Join('|', players
                .OrderBy(p => p.DisplayName)
                .Select(p => $"{p.DisplayName}:{CombatScoreCalculator.TotalScore(p)}"));
        }

        private static List<PlayerCombatStats> ResolvePlayers(
            CombatStatsSnapshot? snapshot,
            bool isRunView) {
            if (isRunView) {
                var run = CombatStatsTracker.RunTotal;
                return run.Players.Values.OrderBy(p => p.DisplayName).ToList();
            }
            return snapshot?.Players.Values.ToList() ?? new List<PlayerCombatStats>();
        }

        private void ClearList() {
            while (_list.GetChildCount() > 0) {
                var child = _list.GetChild(0);
                _list.RemoveChild(child);
                child.Free();
            }
        }

        private static Control MakePlayerRow(PlayerCombatStats player, int total, int maxScore) {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            string name = ResolvePlayerDisplayName(player);
            var nameLabel = new Label {
                Text = name,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 10);
            nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);

            var bd = CombatScoreCalculator.Breakdown(player);
            float barHeight = Math.Max(6f, SidebarBarHeight * total / (float)maxScore);
            var barColumn = new VBoxContainer {
                CustomMinimumSize = new Vector2(SidebarBarWidth + 8, SidebarBarHeight),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            barColumn.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

            var bar = new VerticalScoreStack();
            bar.CustomMinimumSize = new Vector2(SidebarBarWidth + 4, barHeight);
            bar.SetSegments(ScoreBreakdownSegments(bd), Math.Max(total, 1));
            ApplyBarTooltip(bar, FormatPlayerTooltip(name, total, bd));
            barColumn.AddChild(bar);

            var scoreLabel = new Label {
                Text = total.ToString(),
                HorizontalAlignment = HorizontalAlignment.Right,
                CustomMinimumSize = new Vector2(36, 0),
            };
            scoreLabel.AddThemeFontSizeOverride("font_size", 11);
            scoreLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);

            row.AddChild(nameLabel);
            row.AddChild(barColumn);
            row.AddChild(scoreLabel);
            return row;
        }

        private static Control MakeSinglePlayerCompactColumn(PlayerCombatStats player, int total) {
            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 4);
            column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            column.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            string name = ResolvePlayerDisplayName(player);
            var bd = CombatScoreCalculator.Breakdown(player);
            string tooltip = FormatPlayerTooltip(name, total, bd);

            var bar = new VerticalScoreStack {
                BarWidth = RailCompactBarWidth,
                ShowScaleTicks = true,
            };
            bar.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            bar.CustomMinimumSize = new Vector2(0, RailCompactSingleBarMinHeight);
            bar.SetSegments(ScoreBreakdownSegments(bd), Math.Max(total, 1), showLabels: true);
            ApplyBarTooltip(bar, tooltip);
            column.AddChild(bar);

            var scoreLabel = new Label {
                Text = total.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            scoreLabel.AddThemeFontSizeOverride("font_size", 12);
            scoreLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
            ApplyBarTooltip(scoreLabel, tooltip);
            column.AddChild(scoreLabel);

            ApplyBarTooltip(column, tooltip);
            return column;
        }

        private void BuildLegend() {
            foreach (var kind in new[] { "Damage", "Block", "Debuff", "Buff", "Utility", "Potion", "Synergy" }) {
                int idx = kind switch {
                    "Damage" => 0, "Block" => 1, "Debuff" => 2, "Buff" => 3,
                    "Utility" => 4, "Potion" => 5, _ => 6,
                };
                _legend.AddChild(MakeLegendSwatch(LocalizeScoreKind(kind), ScoreKindColor(idx)));
            }
        }

        private static Control MakeLegendSwatch(string label, Color color) {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            row.AddChild(new ColorRect {
                Color = color,
                CustomMinimumSize = new Vector2(8, 8),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            });
            var l = new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            l.AddThemeFontSizeOverride("font_size", 9);
            l.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            row.AddChild(l);
            return row;
        }
    }

    /// <summary>Vertical stacked bar: segment heights sum to total score.</summary>
    private sealed partial class VerticalScoreStack : Control {
        private readonly List<(string Key, int Amount, Color Color)> _segments = new();
        private int _total = 1;

        private const float TickLength = 4f;
        private const float TickGap = 5f;
        private const float LabelOutset = 6f;
        private const float RightNumberGap = 4f;
        private const float TopTickInset = 4f;
        private const float TopLabelInset = 2f;
        private const int TickFontSize = 12;

        public VerticalScoreStack() {
            MouseFilter = MouseFilterEnum.Stop;
        }

        public float BarWidth { get; set; } = SidebarBarWidth;
        public bool ShowScaleTicks { get; set; }

        public void SetSegments(
            IReadOnlyList<(string Key, int Amount, Color Color)> segments,
            int total,
            bool showLabels = false) {
            _segments.Clear();
            foreach (var (key, amount, color) in segments)
                _segments.Add((key, amount, color));
            _total = Math.Max(total, 1);
            ShowScaleTicks = showLabels || ShowScaleTicks;
            QueueRedraw();
        }

        public override void _Draw() {
            float w = Size.X;
            float h = Size.Y;
            float barW = BarWidth;
            if (h < 4f || _segments.Count == 0) {
                DrawRect(new Rect2((w - barW) * 0.5f, 0, barW, h),
                    new Color(0.22f, 0.22f, 0.26f, 0.85f));
                return;
            }

            float x = (w - barW) * 0.5f;
            float y = h;
            foreach (var (_, amount, color) in _segments) {
                float segH = h * amount / _total;
                y -= segH;
                DrawRect(new Rect2(x, y, barW, segH), color);
            }

            if (ShowScaleTicks)
                DrawScaleTicks(x, barW, w, h);
        }

        private void DrawScaleTicks(float barX, float barW, float totalW, float h) {
            var major = new Color(KitLibTheme.Subtle, 0.65f);
            var labelColor = new Color(KitLibTheme.TextSecondary, 0.92f);
            string totalLabel = "TOT";

            DrawTickWithLabels(barX, barW, totalW, TopTickInset, major, labelColor, totalLabel, _total.ToString(), nearTop: true);

            float y = h;
            for (int i = 0; i < _segments.Count; i++) {
                var (key, amount, _) = _segments[i];
                y -= h * amount / (float)_total;
                if (amount <= 0 || y <= 1.5f || y >= h - 1.5f)
                    continue;
                DrawTickWithLabels(
                    barX, barW, totalW, y, major, labelColor,
                    ScoreKindAbbrev(key), amount.ToString());
            }
        }

        private void DrawTickWithLabels(
            float barX,
            float barW,
            float totalW,
            float y,
            Color tickColor,
            Color labelColor,
            string leftText,
            string rightText,
            bool nearTop = false) {
            DrawTickLine(barX, barW, y, tickColor);
            float leftX = Math.Max(1f, barX - TickGap - TickLength - LabelOutset);
            float rightX = barX + barW + RightNumberGap;
            DrawVerticalLabel(leftX, y, leftText, labelColor, nearTop: nearTop);
            DrawVerticalLabel(rightX, y, rightText, labelColor, nearTop: nearTop);
        }

        private void DrawVerticalLabel(
            float anchorX,
            float y,
            string text,
            Color color,
            bool nearTop = false) {
            if (string.IsNullOrEmpty(text))
                return;

            var font = ThemeDB.FallbackFont;
            var size = font.GetStringSize(text, HorizontalAlignment.Left, -1, TickFontSize);
            float textLen = size.X;
            float angle = Mathf.Pi / 2f;
            Vector2 origin = nearTop
                ? new Vector2(anchorX, TopLabelInset + textLen * 0.5f)
                : new Vector2(anchorX, y - textLen * 0.5f);

            DrawSetTransformMatrix(new Transform2D(angle, origin));
            DrawString(font, Vector2.Zero, text, HorizontalAlignment.Center, -1, TickFontSize, color);
            DrawSetTransformMatrix(Transform2D.Identity);
        }

        private void DrawTickLine(float barX, float barW, float y, Color color) {
            float leftEnd = barX - TickGap;
            float leftStart = leftEnd - TickLength;
            float rightStart = barX + barW + 1f;
            float rightEnd = barX + barW + TickGap;
            DrawLine(new Vector2(leftStart, y), new Vector2(leftEnd, y), color, 1f);
            DrawLine(new Vector2(rightStart, y), new Vector2(rightEnd, y), color, 1f);
        }

        public override void _Notification(int what) {
            if (what == NotificationResized)
                QueueRedraw();
        }
    }
}
