using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;

namespace KitLib.CombatStats;

internal static class CombatStatsExport {
    private const string JsonPlaceholder = "__KITLIB_COMBAT_STATS_EMBED__";
    private const string DataScriptOpen = "<script type=\"application/json\" id=\"combat-stats-data\">";
    private const string ShellResourceName = "KitLib.CombatStats.viewer-shell.html";

    private static readonly JsonSerializerOptions SerializerOptions = new() {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    internal static JsonSerializerOptions JsonOptions => SerializerOptions;

    public static string ToJson(CombatStatsLiveDto live) =>
        JsonSerializer.Serialize(live, JsonOptions);

    public static string ToJson(CombatStatsBundle bundle) =>
        JsonSerializer.Serialize(bundle, JsonOptions);

    public static string ToHtml(CombatStatsBundle bundle) {
        var live = new CombatStatsLiveDto {
            Active = bundle.Current ?? bundle.Last,
            IsActive = bundle.Current?.IsActive ?? false,
        };
        return ToHtml(live);
    }

    public static string ToHtml(CombatStatsLiveDto live) {
        string shell = LoadViewerShell();
        string json = SanitizeJsonForHtml(ToJson(live));
        return InjectCombatStatsJson(shell, json);
    }

    public static string WriteHtmlReport(CombatStatsBundle bundle, string? directory = null) {
        directory ??= Path.Combine(OS.GetUserDataDir(), "mod_data", "KitLib");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"combat-stats-{DateTime.Now:yyyyMMdd-HHmmss}.html");
        File.WriteAllText(path, ToHtml(bundle), Encoding.UTF8);
        return path;
    }

    public static string WriteHtmlReport(string? directory = null) =>
        WriteHtmlReport(CaptureBundle(), directory);

    public static string WriteJsonReport(string? directory = null) {
        directory ??= Path.Combine(OS.GetUserDataDir(), "mod_data", "KitLib");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"combat-stats-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.WriteAllText(path, ToJson(CaptureBundle()), Encoding.UTF8);
        return path;
    }

    public static string OpenInBrowser() => DevViewerServer.OpenInBrowser("combat", force: true);

    public static string LoadLiveViewerShell() {
        string shell = LoadViewerShell();
        return InjectCombatStatsJson(shell, "{}");
    }

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

    public static CombatStatsLiveDto CaptureLive() {
        CombatStatsSnapshot? active = CombatStatsTracker.IsTracking
            ? CombatStatsTracker.Current
            : CombatStatsTracker.Last;

        return new CombatStatsLiveDto {
            Active = active == null ? null : CombatStatsSnapshotDto.From(active),
            IsActive = active?.IsActive ?? false,
        };
    }

    public static CombatStatsBundle CaptureBundle() =>
        CombatStatsBundle.From(
            CombatStatsTracker.IsTracking ? CombatStatsTracker.Current : null,
            CombatStatsTracker.Last,
            CombatStatsTracker.RunTotal,
            CombatStatsTracker.RunCombatCount);

    private static string LoadViewerShell() {
        var assembly = typeof(CombatStatsExport).Assembly;
        using var stream = assembly.GetManifestResourceStream(ShellResourceName);
        if (stream == null)
            throw new InvalidOperationException(
                $"Combat stats viewer shell is missing ({ShellResourceName}). Run: cd tools/dev-viewer && pnpm build");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string SanitizeJsonForHtml(string json) =>
        json.Replace("</", "<\\/", StringComparison.Ordinal);

    private static string InjectCombatStatsJson(string shell, string json) {
        int openIdx = shell.IndexOf(DataScriptOpen, StringComparison.Ordinal);
        if (openIdx < 0)
            throw new InvalidOperationException("Combat stats viewer shell is missing the data script tag.");

        int contentStart = openIdx + DataScriptOpen.Length;
        int closeIdx = shell.IndexOf("</script>", contentStart, StringComparison.Ordinal);
        if (closeIdx < 0)
            throw new InvalidOperationException("Combat stats viewer shell data script tag is malformed.");

        string current = shell.Substring(contentStart, closeIdx - contentStart);
        if (current != JsonPlaceholder && !current.StartsWith('{'))
            throw new InvalidOperationException("Combat stats viewer shell has unexpected data script content.");

        return shell.Substring(0, contentStart) + json + shell.Substring(closeIdx);
    }

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

internal sealed class CombatStatsLiveDto {
    public CombatStatsSnapshotDto? Active { get; init; }
    public bool IsActive { get; init; }
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
    public List<CombatStatEventDto> CombatEvents { get; init; } = new();
    public List<TurnSnapshotDto> TurnSnapshots { get; init; } = new();
    public List<CreatureStateDto> LiveCreatures { get; init; } = new();

    public static CombatStatsSnapshotDto From(CombatStatsSnapshot snap) => new() {
        EncounterKey = snap.EncounterKey,
        IsActive = snap.IsActive,
        MaxTurn = snap.MaxTurn,
        Players = snap.Players.Values.Select(PlayerCombatStatsDto.From).ToList(),
        CombatEvents = snap.CombatEvents.Select(CombatStatEventDto.From).ToList(),
        TurnSnapshots = snap.TurnSnapshots.Select(TurnSnapshotDto.From).ToList(),
        LiveCreatures = snap.LiveCreatures.Select(CreatureStateDto.From).ToList(),
    };
}

internal sealed class TurnSnapshotDto {
    public int Turn { get; init; }
    public string Phase { get; init; } = "start";
    public List<CreatureStateDto> Creatures { get; init; } = new();

    public static TurnSnapshotDto From(TurnSnapshot snap) => new() {
        Turn = snap.Turn,
        Phase = snap.Phase,
        Creatures = snap.Creatures.Select(CreatureStateDto.From).ToList(),
    };
}

internal sealed class CreatureStateDto {
    public string Key { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Side { get; init; } = "";
    public int CurrentHp { get; init; }
    public int MaxHp { get; init; }
    public int Block { get; init; }
    public int? Energy { get; init; }
    public List<PowerStateDto> Powers { get; init; } = new();
    public string? IntentSummary { get; init; }

    public static CreatureStateDto From(CreatureState state) => new() {
        Key = state.Key,
        DisplayName = state.DisplayName,
        Side = state.Side,
        CurrentHp = state.CurrentHp,
        MaxHp = state.MaxHp,
        Block = state.Block,
        Energy = state.Energy,
        IntentSummary = state.IntentSummary,
        Powers = state.Powers.Select(p => new PowerStateDto {
            Id = p.Id,
            DisplayName = string.IsNullOrWhiteSpace(p.DisplayName) ? p.Id : p.DisplayName,
            Amount = p.Amount,
        }).ToList(),
    };
}

internal sealed class PowerStateDto {
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int Amount { get; init; }
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
        DamageByCard = new Dictionary<string, int>(p.DamageByCard),
        DamageTakenBySource = new Dictionary<string, int>(p.DamageTakenBySource),
        DamagePerTurn = new Dictionary<string, int>(p.DamagePerTurn),
        BlockByCard = new Dictionary<string, int>(p.BlockByCard),
        PotionUseCount = new Dictionary<string, int>(p.PotionUseCount),
        DebuffsByPower = new Dictionary<string, int>(p.DebuffsByPower),
        PowerDamageBySource = new Dictionary<string, int>(p.PowerDamageBySource),
        Events = p.Events.Select(CombatStatEventDto.From).ToList(),
    };
}

internal sealed class CombatStatEventDto {
    public int Sequence { get; init; }
    public int Turn { get; init; }
    public string Kind { get; init; } = "";
    public string Text { get; init; } = "";
    public int Amount { get; init; }
    public string ActorKey { get; init; } = "";
    public string ActorSide { get; init; } = "";
    public string ActorName { get; init; } = "";
    public string StatePhase { get; init; } = "";
    public CreatureStateDto? Creature { get; init; }
    public bool LinkedToCardPlay { get; init; }
    public string SourceKind { get; init; } = "";
    public string SourceKey { get; init; } = "";
    public string SourceName { get; init; } = "";

    public static CombatStatEventDto From(CombatStatEvent e) => new() {
        Sequence = e.Sequence,
        Turn = e.Turn,
        Kind = e.Kind.ToString(),
        Text = CombatStatsDisplayNames.LocalizeEventText(e.Text),
        Amount = e.Amount,
        ActorKey = e.ActorKey,
        ActorSide = e.ActorSide,
        ActorName = e.ActorName,
        StatePhase = e.StatePhase,
        Creature = e.Creature == null ? null : CreatureStateDto.From(e.Creature),
        LinkedToCardPlay = e.LinkedToCardPlay,
        SourceKind = e.SourceKind.ToString(),
        SourceKey = e.SourceKey,
        SourceName = CombatStatsDisplayNames.LocalizeSourceName(e.SourceKind, e.SourceName),
    };
}
