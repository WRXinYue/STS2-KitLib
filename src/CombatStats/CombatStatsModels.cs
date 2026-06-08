using System.Collections.Generic;
using System.Linq;

namespace KitLib.CombatStats;

internal enum CombatStatEventKind {
    DamageDealt,
    DamageTaken,
    BlockGained,
    CardPlayed,
    EnergySpent,
    PotionUsed,
    DebuffApplied,
    BuffApplied,
    PowerSynergy,
    EnemyMove,
}

internal sealed class CombatStatEvent {
    public int Turn { get; init; }
    public CombatStatEventKind Kind { get; init; }
    public string Text { get; init; } = "";
    public int Amount { get; init; }
    /// <summary>Contribution points for this line (SW-style combat score).</summary>
    public int ScorePoints { get; init; }
}

internal sealed class PlayerCombatStats {
    public string Key { get; init; } = "";
    public string DisplayName { get; set; } = "";
    public string CharacterId { get; set; } = "";

    public int DamageDealt { get; set; }
    public int DamageTaken { get; set; }
    public int BlockGained { get; set; }
    public int CardsPlayed { get; set; }
    public int HitCount { get; set; }

    public int OverkillDealt { get; set; }
    public int BlockedByTarget { get; set; }
    public int DamageBlockedOnTaken { get; set; }

    public int EnergySpent { get; set; }
    public int PotionsUsed { get; set; }
    public int DebuffsApplied { get; set; }
    public int BuffsApplied { get; set; }

    public Dictionary<string, int> DamageByCard { get; } = new();
    public Dictionary<string, int> DamageTakenBySource { get; } = new();
    public Dictionary<string, int> DamagePerTurn { get; } = new();
    public Dictionary<string, int> BlockByCard { get; } = new();
    public Dictionary<string, int> EnergySpentByCard { get; } = new();
    public Dictionary<string, int> PotionUseCount { get; } = new();
    public Dictionary<string, int> DebuffsByPower { get; } = new();
    public Dictionary<string, int> PowerDamageBySource { get; } = new();

    public List<CombatStatEvent> Events { get; } = new();

    public void MergeFrom(PlayerCombatStats other) {
        DamageDealt += other.DamageDealt;
        DamageTaken += other.DamageTaken;
        BlockGained += other.BlockGained;
        CardsPlayed += other.CardsPlayed;
        HitCount += other.HitCount;
        OverkillDealt += other.OverkillDealt;
        BlockedByTarget += other.BlockedByTarget;
        DamageBlockedOnTaken += other.DamageBlockedOnTaken;
        EnergySpent += other.EnergySpent;
        PotionsUsed += other.PotionsUsed;
        DebuffsApplied += other.DebuffsApplied;
        BuffsApplied += other.BuffsApplied;
        MergeDict(DamageByCard, other.DamageByCard);
        MergeDict(DamageTakenBySource, other.DamageTakenBySource);
        MergeDict(DamagePerTurn, other.DamagePerTurn);
        MergeDict(BlockByCard, other.BlockByCard);
        MergeDict(EnergySpentByCard, other.EnergySpentByCard);
        MergeDict(PotionUseCount, other.PotionUseCount);
        MergeDict(DebuffsByPower, other.DebuffsByPower);
        MergeDict(PowerDamageBySource, other.PowerDamageBySource);
    }

    public PlayerCombatStats CloneShallow() {
        var copy = new PlayerCombatStats {
            Key = Key,
            DisplayName = DisplayName,
            CharacterId = CharacterId,
            DamageDealt = DamageDealt,
            DamageTaken = DamageTaken,
            BlockGained = BlockGained,
            CardsPlayed = CardsPlayed,
            HitCount = HitCount,
            OverkillDealt = OverkillDealt,
            BlockedByTarget = BlockedByTarget,
            DamageBlockedOnTaken = DamageBlockedOnTaken,
            EnergySpent = EnergySpent,
            PotionsUsed = PotionsUsed,
            DebuffsApplied = DebuffsApplied,
            BuffsApplied = BuffsApplied,
        };
        CopyDict(DamageByCard, copy.DamageByCard);
        CopyDict(DamageTakenBySource, copy.DamageTakenBySource);
        CopyDict(DamagePerTurn, copy.DamagePerTurn);
        CopyDict(BlockByCard, copy.BlockByCard);
        CopyDict(EnergySpentByCard, copy.EnergySpentByCard);
        CopyDict(PotionUseCount, copy.PotionUseCount);
        CopyDict(DebuffsByPower, copy.DebuffsByPower);
        CopyDict(PowerDamageBySource, copy.PowerDamageBySource);
        copy.Events.AddRange(Events);
        return copy;
    }

    private static void MergeDict(Dictionary<string, int> dst, Dictionary<string, int> src) {
        foreach (var (k, v) in src)
            dst[k] = dst.GetValueOrDefault(k) + v;
    }

    private static void CopyDict<TKey>(Dictionary<TKey, int> src, Dictionary<TKey, int> dst)
        where TKey : notnull {
        foreach (var (k, v) in src)
            dst[k] = v;
    }
}

internal sealed class CombatStatsSnapshot {
    public string EncounterKey { get; set; } = "";
    public bool IsActive { get; set; }
    public int MaxTurn { get; set; }
    public Dictionary<string, PlayerCombatStats> Players { get; } = new();

    public PlayerCombatStats? PrimaryPlayer {
        get {
            if (Players.Count == 0) return null;
            return Players.Values.FirstOrDefault();
        }
    }

    public CombatStatsSnapshot Clone() {
        var copy = new CombatStatsSnapshot {
            EncounterKey = EncounterKey,
            IsActive = IsActive,
            MaxTurn = MaxTurn,
        };
        foreach (var (key, src) in Players)
            copy.Players[key] = src.CloneShallow();
        return copy;
    }

    public void MergeInto(CombatStatsSnapshot totals) {
        totals.MaxTurn = System.Math.Max(totals.MaxTurn, MaxTurn);
        foreach (var (key, src) in Players) {
            if (!totals.Players.TryGetValue(key, out var dst)) {
                totals.Players[key] = src.CloneShallow();
                continue;
            }
            dst.MergeFrom(src);
        }
    }
}
