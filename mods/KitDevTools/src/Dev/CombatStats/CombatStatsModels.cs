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
    CreatureState,
}

internal sealed class CombatStatEvent {
    public int Sequence { get; init; }
    public int Turn { get; init; }
    public CombatStatEventKind Kind { get; init; }
    public string Text { get; init; } = "";
    public int Amount { get; init; }
    public string ActorKey { get; init; } = "";
    public string ActorSide { get; init; } = "";
    public string ActorName { get; init; } = "";
    public string StatePhase { get; init; } = "";
    public CreatureState? Creature { get; init; }
    public bool LinkedToCardPlay { get; set; }
    public CombatStatSourceKind SourceKind { get; init; } = CombatStatSourceKind.Unknown;
    public string SourceKey { get; init; } = "";
    public string SourceName { get; init; } = "";
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

internal sealed class PowerState {
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int Amount { get; init; }
}

internal sealed class CreatureState {
    public string Key { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Side { get; init; } = "";
    public int CurrentHp { get; init; }
    public int MaxHp { get; init; }
    public int Block { get; init; }
    public int? Energy { get; init; }
    public List<PowerState> Powers { get; init; } = new();
    public string? IntentSummary { get; init; }
}

internal sealed class TurnSnapshot {
    public int Turn { get; init; }
    public string Phase { get; init; } = "start";
    public List<CreatureState> Creatures { get; init; } = new();
}

internal sealed class CombatStatsSnapshot {
    public string EncounterKey { get; set; } = "";
    public bool IsActive { get; set; }
    public int MaxTurn { get; set; }
    public Dictionary<string, PlayerCombatStats> Players { get; } = new();
    public List<CombatStatEvent> CombatEvents { get; } = new();
    public List<TurnSnapshot> TurnSnapshots { get; } = new();
    public List<CreatureState> LiveCreatures { get; } = new();

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
        copy.CombatEvents.AddRange(CombatEvents);
        foreach (var turn in TurnSnapshots) {
            copy.TurnSnapshots.Add(new TurnSnapshot {
                Turn = turn.Turn,
                Phase = turn.Phase,
                Creatures = turn.Creatures.Select(CloneCreatureState).ToList(),
            });
        }
        copy.LiveCreatures.AddRange(LiveCreatures.Select(CloneCreatureState));
        return copy;
    }

    private static CreatureState CloneCreatureState(CreatureState src) => new() {
        Key = src.Key,
        DisplayName = src.DisplayName,
        Side = src.Side,
        CurrentHp = src.CurrentHp,
        MaxHp = src.MaxHp,
        Block = src.Block,
        Energy = src.Energy,
        IntentSummary = src.IntentSummary,
        Powers = src.Powers.Select(p => new PowerState {
            Id = p.Id,
            DisplayName = p.DisplayName,
            Amount = p.Amount,
        }).ToList(),
    };

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
