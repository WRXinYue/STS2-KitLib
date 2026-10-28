using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.CombatStats;

/// <summary>
/// Persists a run's accumulated combat totals beside the mod's user data, keyed by the run seed,
/// so continuing or reloading a run keeps the band's history instead of starting from zero.
/// </summary>
internal static class RunStatsStore {
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string Dir => Path.Combine(DataPaths.BaseDir, "run-stats");

    /// <summary>Stable across save/reload; empty when the run exposes no seed.</summary>
    internal static string KeyFor(RunState state) => state.Rng?.StringSeed ?? "";

    internal static void Persist(string? key, CombatStatsSnapshot total, int combatCount) {
        if (string.IsNullOrEmpty(key))
            return;

        try {
            var file = new RunTotalsFile {
                Seed = key,
                CombatCount = combatCount,
                Players = total.Players.Values.Select(PlayerTotalsDto.From).ToList(),
            };
            Directory.CreateDirectory(Dir);
            string path = PathFor(key);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(file, JsonOpts));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception e) {
            GD.PrintErr($"[CombatStats] run totals persist failed: {e.Message}");
        }
    }

    /// <summary>Fills <paramref name="total"/> from the saved file; returns false when there is none.
    /// Players are paired by roster order, because the runtime player keys (net ids) can change
    /// when the same run is loaded again.</summary>
    internal static bool TryRestore(string? key, CombatStatsSnapshot total, out int combatCount) {
        combatCount = 0;
        if (string.IsNullOrEmpty(key))
            return false;

        try {
            string path = PathFor(key);
            if (!File.Exists(path))
                return false;

            var file = JsonSerializer.Deserialize<RunTotalsFile>(File.ReadAllText(path), JsonOpts);
            if (file == null)
                return false;

            var keys = total.Players.Keys.ToList();
            if (file.Players.Count != keys.Count)
                return false;

            for (int i = 0; i < keys.Count; i++)
                file.Players[i].CopyTo(total.Players[keys[i]]);

            combatCount = file.CombatCount;
            return true;
        }
        catch (Exception e) {
            GD.PrintErr($"[CombatStats] run totals restore failed: {e.Message}");
            return false;
        }
    }

    private static string PathFor(string key) {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(key.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return Path.Combine(Dir, safe + ".json");
    }

    private sealed class RunTotalsFile {
        public int Version { get; set; } = 1;
        public string Seed { get; set; } = "";
        public int CombatCount { get; set; }
        public List<PlayerTotalsDto> Players { get; set; } = new();
    }

    private sealed class PlayerTotalsDto {
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

        public Dictionary<string, int> DamageByCard { get; set; } = new();
        public Dictionary<string, int> DamageTakenBySource { get; set; } = new();
        public Dictionary<string, int> DamagePerTurn { get; set; } = new();
        public Dictionary<string, int> BlockByCard { get; set; } = new();
        public Dictionary<string, int> EnergySpentByCard { get; set; } = new();
        public Dictionary<string, int> PotionUseCount { get; set; } = new();
        public Dictionary<string, int> DebuffsByPower { get; set; } = new();
        public Dictionary<string, int> PowerDamageBySource { get; set; } = new();

        internal static PlayerTotalsDto From(PlayerCombatStats stats) => new() {
            DisplayName = stats.DisplayName,
            CharacterId = stats.CharacterId,
            DamageDealt = stats.DamageDealt,
            DamageTaken = stats.DamageTaken,
            BlockGained = stats.BlockGained,
            CardsPlayed = stats.CardsPlayed,
            HitCount = stats.HitCount,
            OverkillDealt = stats.OverkillDealt,
            BlockedByTarget = stats.BlockedByTarget,
            DamageBlockedOnTaken = stats.DamageBlockedOnTaken,
            EnergySpent = stats.EnergySpent,
            PotionsUsed = stats.PotionsUsed,
            DebuffsApplied = stats.DebuffsApplied,
            BuffsApplied = stats.BuffsApplied,
            DamageByCard = new(stats.DamageByCard),
            DamageTakenBySource = new(stats.DamageTakenBySource),
            DamagePerTurn = new(stats.DamagePerTurn),
            BlockByCard = new(stats.BlockByCard),
            EnergySpentByCard = new(stats.EnergySpentByCard),
            PotionUseCount = new(stats.PotionUseCount),
            DebuffsByPower = new(stats.DebuffsByPower),
            PowerDamageBySource = new(stats.PowerDamageBySource),
        };

        internal void CopyTo(PlayerCombatStats stats) {
            stats.DisplayName = DisplayName;
            stats.CharacterId = CharacterId;
            stats.DamageDealt = DamageDealt;
            stats.DamageTaken = DamageTaken;
            stats.BlockGained = BlockGained;
            stats.CardsPlayed = CardsPlayed;
            stats.HitCount = HitCount;
            stats.OverkillDealt = OverkillDealt;
            stats.BlockedByTarget = BlockedByTarget;
            stats.DamageBlockedOnTaken = DamageBlockedOnTaken;
            stats.EnergySpent = EnergySpent;
            stats.PotionsUsed = PotionsUsed;
            stats.DebuffsApplied = DebuffsApplied;
            stats.BuffsApplied = BuffsApplied;
            CopyDict(DamageByCard, stats.DamageByCard);
            CopyDict(DamageTakenBySource, stats.DamageTakenBySource);
            CopyDict(DamagePerTurn, stats.DamagePerTurn);
            CopyDict(BlockByCard, stats.BlockByCard);
            CopyDict(EnergySpentByCard, stats.EnergySpentByCard);
            CopyDict(PotionUseCount, stats.PotionUseCount);
            CopyDict(DebuffsByPower, stats.DebuffsByPower);
            CopyDict(PowerDamageBySource, stats.PowerDamageBySource);
        }

        private static void CopyDict(Dictionary<string, int> from, Dictionary<string, int> to) {
            to.Clear();
            foreach (var pair in from)
                to[pair.Key] = pair.Value;
        }
    }
}
