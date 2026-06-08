using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitLib.CombatStats;

internal static class CombatStatsExport {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ToJson(CombatStatsBundle bundle) =>
        JsonSerializer.Serialize(bundle, JsonOptions);

    public static string ToTextSummary(CombatStatsBundle bundle) {
        var sb = new StringBuilder(512);
        sb.AppendLine("=== DevMode Combat Stats ===");
        sb.AppendLine($"Generated: {DateTime.Now:O}");
        sb.AppendLine();

        AppendSnapshot(sb, "Current combat", bundle.Current);
        AppendSnapshot(sb, "Last combat", bundle.Last);
        AppendSnapshot(sb, $"Run total ({bundle.RunCombatCount} combats)", bundle.RunTotal);

        return sb.ToString();
    }

    public static CombatStatsBundle CaptureBundle() =>
        CombatStatsBundle.From(
            CombatStatsTracker.IsTracking ? CombatStatsTracker.Current : null,
            CombatStatsTracker.Last,
            CombatStatsTracker.RunTotal,
            CombatStatsTracker.RunCombatCount);

    private static void AppendSnapshot(StringBuilder sb, string label, CombatStatsSnapshotDto? snap) {
        sb.AppendLine($"--- {label} ---");
        if (snap == null || snap.Players.Count == 0) {
            sb.AppendLine("(empty)");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"Encounter: {snap.EncounterKey}");
        sb.AppendLine($"Active: {snap.IsActive}  Turns: {snap.MaxTurn}");
        foreach (var p in snap.Players) {
            sb.AppendLine($"[{p.DisplayName}] dealt={p.DamageDealt} taken={p.DamageTaken} block={p.BlockGained} " +
                          $"overkill={p.OverkillDealt} blockedByTarget={p.BlockedByTarget} " +
                          $"energy={p.EnergySpent} potions={p.PotionsUsed} debuffs={p.DebuffsApplied}");
        }
        sb.AppendLine();
    }
}

internal sealed class CombatStatsBundle {
    public CombatStatsSnapshotDto? Current { get; init; }
    public CombatStatsSnapshotDto? Last { get; init; }
    public CombatStatsSnapshotDto? RunTotal { get; init; }
    public int RunCombatCount { get; init; }

    public static CombatStatsBundle From(CombatStatsSnapshot? current, CombatStatsSnapshot? last,
        CombatStatsSnapshot runTotal, int runCombatCount) => new() {
        Current = current == null ? null : CombatStatsSnapshotDto.From(current),
        Last = last == null ? null : CombatStatsSnapshotDto.From(last),
        RunTotal = CombatStatsSnapshotDto.From(runTotal),
        RunCombatCount = runCombatCount,
    };
}

internal sealed class CombatStatsSnapshotDto {
    public string EncounterKey { get; init; } = "";
    public bool IsActive { get; init; }
    public int MaxTurn { get; init; }
    public List<PlayerCombatStatsDto> Players { get; init; } = new();

    public static CombatStatsSnapshotDto From(CombatStatsSnapshot snap) => new() {
        EncounterKey = snap.EncounterKey,
        IsActive = snap.IsActive,
        MaxTurn = snap.MaxTurn,
        Players = snap.Players.Values.Select(PlayerCombatStatsDto.From).ToList(),
    };
}

internal sealed class PlayerCombatStatsDto {
    public string Key { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string CharacterId { get; init; } = "";
    public int DamageDealt { get; init; }
    public int DamageTaken { get; init; }
    public int BlockGained { get; init; }
    public int CardsPlayed { get; init; }
    public int HitCount { get; init; }
    public int OverkillDealt { get; init; }
    public int BlockedByTarget { get; init; }
    public int DamageBlockedOnTaken { get; init; }
    public int EnergySpent { get; init; }
    public int PotionsUsed { get; init; }
    public int DebuffsApplied { get; init; }
    public int BuffsApplied { get; init; }
    public int CombatScore { get; init; }
    public Dictionary<string, int> DamageByCard { get; init; } = new();
    public Dictionary<string, int> DamageTakenBySource { get; init; } = new();
    public Dictionary<string, int> DamagePerTurn { get; init; } = new();
    public Dictionary<string, int> BlockByCard { get; init; } = new();
    public Dictionary<string, int> PotionUseCount { get; init; } = new();
    public Dictionary<string, int> DebuffsByPower { get; init; } = new();
    public Dictionary<string, int> PowerDamageBySource { get; init; } = new();
    public List<CombatStatEventDto> Events { get; init; } = new();

    public static PlayerCombatStatsDto From(PlayerCombatStats p) => new() {
        Key = p.Key,
        DisplayName = p.DisplayName,
        CharacterId = p.CharacterId,
        DamageDealt = p.DamageDealt,
        DamageTaken = p.DamageTaken,
        BlockGained = p.BlockGained,
        CardsPlayed = p.CardsPlayed,
        HitCount = p.HitCount,
        OverkillDealt = p.OverkillDealt,
        BlockedByTarget = p.BlockedByTarget,
        DamageBlockedOnTaken = p.DamageBlockedOnTaken,
        EnergySpent = p.EnergySpent,
        PotionsUsed = p.PotionsUsed,
        DebuffsApplied = p.DebuffsApplied,
        BuffsApplied = p.BuffsApplied,
        CombatScore = CombatScoreCalculator.TotalScore(p),
        DamageByCard = new Dictionary<string, int>(p.DamageByCard),
        DamageTakenBySource = new Dictionary<string, int>(p.DamageTakenBySource),
        DamagePerTurn = new Dictionary<string, int>(p.DamagePerTurn),
        BlockByCard = new Dictionary<string, int>(p.BlockByCard),
        PotionUseCount = new Dictionary<string, int>(p.PotionUseCount),
        DebuffsByPower = new Dictionary<string, int>(p.DebuffsByPower),
        PowerDamageBySource = new Dictionary<string, int>(p.PowerDamageBySource),
        Events = p.Events.Select(e => new CombatStatEventDto {
            Turn = e.Turn,
            Kind = e.Kind.ToString(),
            Text = e.Text,
            Amount = e.Amount,
            ScorePoints = e.ScorePoints,
        }).ToList(),
    };
}

internal sealed class CombatStatEventDto {
    public int Turn { get; init; }
    public string Kind { get; init; } = "";
    public string Text { get; init; } = "";
    public int Amount { get; init; }
    public int ScorePoints { get; init; }
}
