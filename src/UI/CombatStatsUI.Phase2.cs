using System.Linq;
using KitLib.CombatStats;
using Godot;

namespace KitLib.UI;

internal static partial class CombatStatsUI {
    private static void BuildCompareSection(VBoxContainer parent, PlayerCombatStats current, PlayerCombatStats last) {
        parent.AddChild(MakeSectionCard(I18N.T("combatStats.section.compare", "vs last combat"), section => {
            section.AddChild(MakeCompareRow(I18N.T("combatStats.dealt", "Damage dealt"),
                current.DamageDealt, last.DamageDealt));
            section.AddChild(MakeCompareRow(I18N.T("combatStats.taken", "Damage taken"),
                current.DamageTaken, last.DamageTaken));
            section.AddChild(MakeCompareRow(I18N.T("combatStats.block", "Block gained"),
                current.BlockGained, last.BlockGained));
        }));
    }

    private static Control MakeCompareRow(string label, int current, int last) {
        int delta = current - last;
        string sign = delta > 0 ? "+" : "";
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var left = new Label {
            Text = $"{label}:",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        left.AddThemeFontSizeOverride("font_size", 11);
        left.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        var right = new Label {
            Text = $"{current} ({sign}{delta})",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        right.AddThemeFontSizeOverride("font_size", 11);
        right.AddThemeColorOverride("font_color",
            delta > 0 ? KitLibTheme.Accent : KitLibTheme.TextSecondary);
        row.AddChild(left);
        row.AddChild(right);
        return row;
    }

    private static void BuildExtendedView(VBoxContainer parent, PlayerCombatStats player, int maxTurn) {
        var score = CombatScoreCalculator.Breakdown(player);
        parent.AddChild(MakeSectionCard(I18N.T("combatStats.section.score", "Combat score"), section => {
            section.AddChild(MakeValueRow("ext.score", I18N.T("combatStats.score", "Total"), score.Total, false));
            section.AddChild(MakeValueRow("ext.scoreDmg", I18N.T("combatStats.score.damage", "From damage"), score.Damage, false));
            section.AddChild(MakeValueRow("ext.scoreBlock", I18N.T("combatStats.score.block", "From block"), score.Block, false));
            section.AddChild(MakeValueRow("ext.scoreDebuff", I18N.T("combatStats.score.debuff", "From debuffs"), score.Debuff, false));
            section.AddChild(MakeValueRow("ext.scoreBuff", I18N.T("combatStats.score.buff", "From buffs"), score.Buff, false));
            section.AddChild(MakeValueRow("ext.scoreUtil", I18N.T("combatStats.score.utility", "From utility cards"), score.Utility, false));
            section.AddChild(MakeValueRow("ext.scorePotion", I18N.T("combatStats.score.potion", "From potions"), score.Potion, false));
            section.AddChild(MakeValueRow("ext.scoreSyn", I18N.T("combatStats.score.synergy", "From debuff synergy"), score.Synergy, false));
        }));

        parent.AddChild(MakeSectionCard(I18N.T("combatStats.section.offense", "Offense"), section => {
            section.AddChild(MakeValueRow("ext.dealt", I18N.T("combatStats.dealt", "Damage dealt"), player.DamageDealt, false));
            section.AddChild(MakeValueRow("ext.overkill", I18N.T("combatStats.overkill", "Overkill"), player.OverkillDealt, false));
            section.AddChild(MakeValueRow("ext.blockedDeal", I18N.T("combatStats.blockedDeal", "Blocked (dealt)"), player.BlockedByTarget, false));
            section.AddChild(MakeValueRow("ext.hits", I18N.T("combatStats.hits", "Hit count"), player.HitCount, false));
            section.AddChild(MakeValueRow("ext.cards", I18N.T("combatStats.cardsPlayed", "Cards played"), player.CardsPlayed, false));
            section.AddChild(MakeValueRow("ext.energy", I18N.T("combatStats.energy", "Energy spent"), player.EnergySpent, false));
            section.AddChild(MakeValueRow("ext.potions", I18N.T("combatStats.potions", "Potions used"), player.PotionsUsed, false));
            section.AddChild(MakeValueRow("ext.debuffs", I18N.T("combatStats.debuffs", "Debuffs applied"), player.DebuffsApplied, false));
            section.AddChild(MakeValueRow("ext.turns", I18N.T("combatStats.turns", "Turns recorded"), maxTurn, false));
        }));

        parent.AddChild(MakeSectionCard(I18N.T("combatStats.section.defense", "Defense"), section => {
            section.AddChild(MakeValueRow("ext.taken", I18N.T("combatStats.taken", "Damage taken"), player.DamageTaken, false));
            section.AddChild(MakeValueRow("ext.blockedTaken", I18N.T("combatStats.blockedTaken", "Blocked (taken)"), player.DamageBlockedOnTaken, false));
            section.AddChild(MakeValueRow("ext.block", I18N.T("combatStats.block", "Block gained"), player.BlockGained, false));
        }));

        if (player.PowerDamageBySource.Count > 0) {
            parent.AddChild(MakeSectionCard(I18N.T("combatStats.section.powerDmg", "Power damage"), section => {
                foreach (var (name, amount) in player.PowerDamageBySource.OrderByDescending(kv => kv.Value))
                    section.AddChild(MakeBarRow(name, amount, player.DamageDealt, false));
            }));
        }

        if (player.BlockByCard.Count > 0) {
            parent.AddChild(MakeSectionCard(I18N.T("combatStats.section.blockCards", "Block by card"), section => {
                foreach (var (name, amount) in player.BlockByCard.OrderByDescending(kv => kv.Value).Take(12))
                    section.AddChild(MakeBarRow(name, amount, player.BlockGained, false));
            }));
        }

        if (player.PotionUseCount.Count > 0) {
            parent.AddChild(MakeSectionCard(I18N.T("combatStats.section.potions", "Potions"), section => {
                foreach (var (name, count) in player.PotionUseCount.OrderByDescending(kv => kv.Value))
                    section.AddChild(MakeBarRow(name, count, player.PotionsUsed, false));
            }));
        }
    }

    private static void BuildTimelineView(VBoxContainer parent, PlayerCombatStats player) {
        if (player.Events.Count == 0) {
            parent.AddChild(MakeHintLabel(I18N.T("combatStats.noTimeline", "No events recorded yet.")));
            return;
        }

        int total = CombatScoreCalculator.TotalScore(player);
        parent.AddChild(MakeHintLabel(I18N.T("combatStats.timelineHint",
            "Drag to select text and copy. Score weights: damage 1:1, block ×0.65, debuff 12/stack, buff 8/stack, utility card 3+5×energy.")));
        parent.AddChild(MakeValueRow("timeline.score", I18N.T("combatStats.score", "Combat score"), total, false));

        parent.AddChild(MakeSectionCard(I18N.T("combatStats.view.timeline", "Timeline"), section => {
            var rtl = new RichTextLabel {
                Name = "timeline.rtl",
                BbcodeEnabled = false,
                SelectionEnabled = true,
                ScrollActive = false,
                FitContent = true,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Text = BuildTimelineText(player),
            };
            rtl.AddThemeFontSizeOverride("normal_font_size", 10);
            rtl.AddThemeColorOverride("default_color", KitLibTheme.TextSecondary);
            var noFocus = new StyleBoxEmpty();
            rtl.AddThemeStyleboxOverride("normal", noFocus);
            rtl.AddThemeStyleboxOverride("focus", noFocus);
            rtl.CustomMinimumSize = new Vector2(0, 120);
            rtl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            section.AddChild(rtl);
        }));
    }

    private static string BuildTimelineText(PlayerCombatStats player) =>
        string.Join("\n", player.Events.AsEnumerable().Reverse().Take(120)
            .Select(CombatScoreCalculator.FormatTimelineLine));

    private static void RefreshTimeline(VBoxContainer inner, PlayerCombatStats player, bool animate) {
        FindValueRow(inner, "timeline.score")?.SetValue(CombatScoreCalculator.TotalScore(player), animate);
        if (inner.FindChild("timeline.rtl", recursive: true, owned: false) is RichTextLabel rtl)
            rtl.Text = BuildTimelineText(player);
    }

    private static void BuildRunView(VBoxContainer parent) {
        int count = CombatStatsTracker.RunCombatCount;
        var run = CombatStatsTracker.RunTotal;
        var player = run.PrimaryPlayer;

        parent.AddChild(MakeHintLabel(I18N.T("combatStats.runHint", "Combined stats for {0} combat(s) this run.", count)));

        if (player == null || count == 0) {
            parent.AddChild(MakeHintLabel(I18N.T("combatStats.runEmpty", "No completed combats in this run yet.")));
            return;
        }

        BuildExtendedView(parent, player, run.MaxTurn);
    }

    private static void RefreshExtended(VBoxContainer inner, PlayerCombatStats player, int maxTurn, bool animate) {
        var score = CombatScoreCalculator.Breakdown(player);
        FindValueRow(inner, "ext.score")?.SetValue(score.Total, animate);
        FindValueRow(inner, "ext.scoreDmg")?.SetValue(score.Damage, animate);
        FindValueRow(inner, "ext.scoreBlock")?.SetValue(score.Block, animate);
        FindValueRow(inner, "ext.scoreDebuff")?.SetValue(score.Debuff, animate);
        FindValueRow(inner, "ext.scoreBuff")?.SetValue(score.Buff, animate);
        FindValueRow(inner, "ext.scoreUtil")?.SetValue(score.Utility, animate);
        FindValueRow(inner, "ext.scorePotion")?.SetValue(score.Potion, animate);
        FindValueRow(inner, "ext.scoreSyn")?.SetValue(score.Synergy, animate);
        FindValueRow(inner, "ext.dealt")?.SetValue(player.DamageDealt, animate);
        FindValueRow(inner, "ext.overkill")?.SetValue(player.OverkillDealt, animate);
        FindValueRow(inner, "ext.blockedDeal")?.SetValue(player.BlockedByTarget, animate);
        FindValueRow(inner, "ext.hits")?.SetValue(player.HitCount, animate);
        FindValueRow(inner, "ext.cards")?.SetValue(player.CardsPlayed, animate);
        FindValueRow(inner, "ext.energy")?.SetValue(player.EnergySpent, animate);
        FindValueRow(inner, "ext.potions")?.SetValue(player.PotionsUsed, animate);
        FindValueRow(inner, "ext.debuffs")?.SetValue(player.DebuffsApplied, animate);
        FindValueRow(inner, "ext.turns")?.SetValue(maxTurn, animate);
        FindValueRow(inner, "ext.taken")?.SetValue(player.DamageTaken, animate);
        FindValueRow(inner, "ext.blockedTaken")?.SetValue(player.DamageBlockedOnTaken, animate);
        FindValueRow(inner, "ext.block")?.SetValue(player.BlockGained, animate);
    }
}
