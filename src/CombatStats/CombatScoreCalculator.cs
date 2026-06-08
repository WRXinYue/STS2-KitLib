using System;
using System.Collections.Generic;
using System.Linq;

namespace KitLib.CombatStats;

internal enum CombatPieCategory {
    Overview,
    Cards,
    Offense,
    Support,
    Tank,
}

/// <summary>
/// SW-inspired combat contribution score. Direct damage is the baseline (1 pt / 1 dmg);
/// mitigation and setup are discounted but still count toward the total.
/// </summary>
internal static class CombatScoreCalculator {
    /// <summary>Block / shield value vs damage (SW meters often use ~50–70%).</summary>
    public const float BlockWeight = 0.65f;

    /// <summary>Per stack of debuff applied (Vulnerable, Weak, etc.).</summary>
    public const int DebuffPerStack = 12;

    /// <summary>Per stack of buff applied (slightly below debuff — buffs are self-value).</summary>
    public const int BuffPerStack = 8;

    public const int PotionBase = 25;

    /// <summary>Floor for a non-attack card play (skills like Panic / setup).</summary>
    public const int UtilityBaseline = 3;

    /// <summary>Each energy spent on a utility card (SW: expensive setup still matters).</summary>
    public const int UtilityPerEnergy = 5;

    public static int UtilityPlayScore(int energySpent) =>
        UtilityBaseline + Math.Max(0, energySpent) * UtilityPerEnergy;

    public static int DamageScore(int amount) => amount;

    public static int BlockScore(int amount) => (int)Math.Round(amount * BlockWeight);

    public static int DebuffScore(int stacks) => stacks * DebuffPerStack;

    public static int BuffScore(int stacks) => stacks * BuffPerStack;

    public static int PotionScore() => PotionBase;

    /// <summary>Effective damage from debuff/buff synergy (Vulnerable bonus, Weak mitigation).</summary>
    public static int SynergyScore(int amount) => amount;

    public static int TotalScore(PlayerCombatStats player) =>
        player.Events.Sum(e => e.ScorePoints);

    /// <summary>Per-card combat contribution (damage, block, utility plays).</summary>
    public static Dictionary<string, int> CardContributionByKey(PlayerCombatStats player) {
        var map = new Dictionary<string, int>();
        foreach (var (name, damage) in player.DamageByCard)
            AddScore(map, name, DamageScore(damage));
        foreach (var (name, block) in player.BlockByCard)
            AddScore(map, name, BlockScore(block));
        foreach (var ev in player.Events) {
            if (ev.Kind == CombatStatEventKind.CardPlayed && ev.ScorePoints > 0)
                AddScore(map, ev.Text, ev.ScorePoints);
        }
        return map;
    }

    private static void AddScore(Dictionary<string, int> map, string key, int points) {
        if (points <= 0) return;
        map[key] = map.GetValueOrDefault(key) + points;
    }

    public static (Dictionary<string, int> Data, int Total) GetPieCategoryData(
        PlayerCombatStats player,
        CombatPieCategory category) {
        var bd = Breakdown(player);
        if (category == CombatPieCategory.Cards) {
            var cards = CardContributionByKey(player);
            return (cards, Math.Max(cards.Values.Sum(), 1));
        }

        return category switch {
            CombatPieCategory.Overview => (ScoreBreakdownByKind(bd), Math.Max(bd.Total, 1)),
            CombatPieCategory.Offense => (OffenseContributionByKey(player), Math.Max(player.DamageDealt, 1)),
            CombatPieCategory.Support => (SupportContributionByKey(player),
                Math.Max(bd.Block + bd.Utility + bd.Debuff + bd.Buff + bd.Potion + bd.Synergy, 1)),
            CombatPieCategory.Tank => (new Dictionary<string, int>(player.DamageTakenBySource),
                Math.Max(player.DamageTaken, 1)),
            _ => (new Dictionary<string, int>(), 1),
        };
    }

    /// <summary>Combat score split by contribution kind (for overview pie).</summary>
    public static Dictionary<string, int> ScoreBreakdownByKind(CombatScoreBreakdown bd) {
        var map = new Dictionary<string, int>();
        AddScore(map, nameof(bd.Damage), bd.Damage);
        AddScore(map, nameof(bd.Block), bd.Block);
        AddScore(map, nameof(bd.Debuff), bd.Debuff);
        AddScore(map, nameof(bd.Buff), bd.Buff);
        AddScore(map, nameof(bd.Utility), bd.Utility);
        AddScore(map, nameof(bd.Potion), bd.Potion);
        AddScore(map, nameof(bd.Synergy), bd.Synergy);
        return map;
    }

    private static Dictionary<string, int> OffenseContributionByKey(PlayerCombatStats player) {
        var map = new Dictionary<string, int>();
        foreach (var (name, damage) in player.DamageByCard)
            AddScore(map, name, damage);
        foreach (var (name, damage) in player.PowerDamageBySource)
            AddScore(map, name, damage);
        return map;
    }

    /// <summary>Non-damage setup: block, utility, debuffs, potions, synergy credit.</summary>
    public static Dictionary<string, int> SupportContributionByKey(PlayerCombatStats player) {
        var map = new Dictionary<string, int>();
        foreach (var (name, block) in player.BlockByCard)
            AddScore(map, name, BlockScore(block));
        foreach (var ev in player.Events) {
            switch (ev.Kind) {
                case CombatStatEventKind.CardPlayed when ev.ScorePoints > 0:
                    AddScore(map, ev.Text, ev.ScorePoints);
                    break;
                case CombatStatEventKind.DebuffApplied:
                case CombatStatEventKind.BuffApplied:
                case CombatStatEventKind.PotionUsed:
                    AddScore(map, ev.Text, ev.ScorePoints);
                    break;
                case CombatStatEventKind.PowerSynergy:
                    string label = ev.Text;
                    int arrow = label.IndexOf(" → ", StringComparison.Ordinal);
                    if (arrow > 0)
                        label = label[..arrow];
                    AddScore(map, label, ev.ScorePoints);
                    break;
            }
        }
        return map;
    }

    public static CombatScoreBreakdown Breakdown(PlayerCombatStats player) {
        var bd = new CombatScoreBreakdown();
        foreach (var ev in player.Events) {
            switch (ev.Kind) {
                case CombatStatEventKind.DamageDealt:
                    bd.Damage += ev.ScorePoints;
                    break;
                case CombatStatEventKind.BlockGained:
                    bd.Block += ev.ScorePoints;
                    break;
                case CombatStatEventKind.DebuffApplied:
                    bd.Debuff += ev.ScorePoints;
                    break;
                case CombatStatEventKind.BuffApplied:
                    bd.Buff += ev.ScorePoints;
                    break;
                case CombatStatEventKind.CardPlayed:
                    bd.Utility += ev.ScorePoints;
                    break;
                case CombatStatEventKind.PotionUsed:
                    bd.Potion += ev.ScorePoints;
                    break;
                case CombatStatEventKind.PowerSynergy:
                    bd.Synergy += ev.ScorePoints;
                    break;
            }
        }
        return bd;
    }

    /// <summary>
    /// Display breakdown: event stream plus aggregate fallbacks when events are incomplete
    /// (can happen for the local player in multiplayer when history attribution differs).
    /// </summary>
    public static CombatScoreBreakdown BreakdownForDisplay(PlayerCombatStats player) {
        var bd = Breakdown(player);

        int blockFromCards = 0;
        foreach (var (_, amount) in player.BlockByCard)
            blockFromCards += BlockScore(amount);
        if (blockFromCards > bd.Block)
            bd.Block = blockFromCards;

        int damageFromCards = 0;
        foreach (var (_, amount) in player.DamageByCard)
            damageFromCards += DamageScore(amount);
        foreach (var (_, amount) in player.PowerDamageBySource)
            damageFromCards += DamageScore(amount);
        if (damageFromCards > bd.Damage)
            bd.Damage = damageFromCards;

        int debuffFromPowers = 0;
        foreach (var (_, stacks) in player.DebuffsByPower)
            debuffFromPowers += DebuffScore(stacks);
        if (debuffFromPowers > bd.Debuff)
            bd.Debuff = debuffFromPowers;

        if (player.BuffsApplied > 0 && bd.Buff <= 0)
            bd.Buff = BuffScore(player.BuffsApplied);

        int potionScore = 0;
        foreach (var (_, count) in player.PotionUseCount)
            potionScore += PotionScore() * count;
        if (potionScore > bd.Potion)
            bd.Potion = potionScore;

        return bd;
    }

    public static string FormatTimelineLine(CombatStatEvent ev) {
        string line = $"T{ev.Turn} · {CombatStatsDisplayNames.LocalizeEventText(ev.Text)}";
        return ev.ScorePoints > 0 ? $"{line}  (+{ev.ScorePoints})" : line;
    }
}

internal sealed class CombatScoreBreakdown {
    public int Damage { get; set; }
    public int Block { get; set; }
    public int Debuff { get; set; }
    public int Buff { get; set; }
    public int Utility { get; set; }
    public int Potion { get; set; }
    public int Synergy { get; set; }
    public int Total => Damage + Block + Debuff + Buff + Utility + Potion + Synergy;
}
