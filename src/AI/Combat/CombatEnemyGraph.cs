using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

/// <summary>Per-combat summoner links (static priors + runtime spawn observation).</summary>
public static class CombatEnemyGraph {
    static int _lastEnemyCount;
    static readonly Dictionary<int, int> SummonerByIndex = new();
    static string? _lastSignature;

    public static void Reset() {
        _lastEnemyCount = 0;
        SummonerByIndex.Clear();
        _lastSignature = null;
    }

    public static void ObserveAndEnrich(JsonArray enemies) {
        if (enemies == null) return;
        if (enemies.Count == 0) {
            Reset();
            return;
        }

        var signature = BuildSignature(enemies);
        if (_lastSignature != null && !string.Equals(signature, _lastSignature, StringComparison.Ordinal)) {
            if (_lastEnemyCount == 0 && enemies.Count > 0)
                Reset();
        }

        _lastSignature = signature;

        if (enemies.Count > _lastEnemyCount) {
            int summoner = FindLikelySummoner(enemies, _lastEnemyCount, enemies.Count);
            if (summoner >= 0) {
                for (int i = _lastEnemyCount; i < enemies.Count; i++)
                    SummonerByIndex[i] = summoner;
            }
            else {
                LinkNewEnemiesBySpawnPriors(enemies, _lastEnemyCount);
            }
        }

        _lastEnemyCount = enemies.Count;

        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i] is not JsonObject enemy) continue;

            var summonerIndex = ResolveSummonerIndex(enemies, i, enemy);
            if (summonerIndex >= 0)
                enemy["summonerIndex"] = summonerIndex;
        }
    }

    public static int GetSummonerIndex(int enemyIndex) =>
        SummonerByIndex.TryGetValue(enemyIndex, out var idx) ? idx : -1;

    static int ResolveSummonerIndex(JsonArray enemies, int index, JsonObject enemy) {
        if (SummonerByIndex.TryGetValue(index, out var observed))
            return observed;

        if (!EnemyTargetPriority.IsMinion(enemy))
            return -1;

        var monsterId = enemy["monsterId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(monsterId))
            return -1;

        if (!MonsterMechanicIndex.TryGet(monsterId, out var profile))
            return -1;

        for (int i = 0; i < enemies.Count; i++) {
            if (i == index) continue;
            if (enemies[i] is not JsonObject candidate) continue;
            if (!EnemyTargetPriority.IsAlive(candidate)) continue;
            if (EnemyTargetPriority.IsMinion(candidate)) continue;

            var primaryId = candidate["monsterId"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(primaryId)) continue;

            if (profile.SpawnedMonsterIds.Any(id =>
                    string.Equals(id, monsterId, StringComparison.OrdinalIgnoreCase))
                || string.Equals(primaryId, monsterId, StringComparison.OrdinalIgnoreCase) == false
                   && MonsterMechanicIndex.TryGet(primaryId, out var primary)
                   && primary.SpawnedMonsterIds.Any(id =>
                       string.Equals(id, monsterId, StringComparison.OrdinalIgnoreCase)))
                return i;
        }

        return -1;
    }

    static void LinkNewEnemiesBySpawnPriors(JsonArray enemies, int firstNewIndex) {
        for (int i = firstNewIndex; i < enemies.Count; i++) {
            if (enemies[i] is not JsonObject spawned) continue;
            var spawnedId = spawned["monsterId"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(spawnedId)) continue;

            for (int p = 0; p < enemies.Count; p++) {
                if (p == i) continue;
                if (enemies[p] is not JsonObject primary) continue;
                if (!EnemyTargetPriority.IsAlive(primary)) continue;
                if (EnemyTargetPriority.IsMinion(primary)) continue;

                var primaryId = primary["monsterId"]?.GetValue<string>();
                if (!MonsterMechanicIndex.TryGet(primaryId, out var profile)) continue;
                if (!profile.SpawnedMonsterIds.Any(id =>
                        string.Equals(id, spawnedId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                SummonerByIndex[i] = p;
                break;
            }
        }
    }

    static int FindLikelySummoner(JsonArray enemies, int oldCount, int newCount) {
        for (int i = 0; i < oldCount; i++) {
            if (enemies[i] is not JsonObject enemy) continue;
            if (!EnemyTargetPriority.IsAlive(enemy)) continue;

            var moveId = enemy["nextMoveId"]?.GetValue<string>() ?? "";
            if (!moveId.Contains("SUMMON", StringComparison.OrdinalIgnoreCase)
                && !moveId.Contains("ILLUSION", StringComparison.OrdinalIgnoreCase)
                && !moveId.Contains("LAY_EGG", StringComparison.OrdinalIgnoreCase)
                && !moveId.Contains("FABRICATE", StringComparison.OrdinalIgnoreCase)
                && !moveId.Contains("BLOAT", StringComparison.OrdinalIgnoreCase))
                continue;

            var tags = enemy["intentTags"]?.AsArray();
            if (tags != null && tags.Any(t =>
                    string.Equals(t?.GetValue<string>(), "Summon", StringComparison.OrdinalIgnoreCase)))
                return i;

            var monsterId = enemy["monsterId"]?.GetValue<string>();
            if (MonsterMechanicIndex.TryGet(monsterId, out var profile)
                && profile.Flags.HasFlag(EnemyMechanicFlags.CanSummonAllies))
                return i;
        }

        for (int i = 0; i < oldCount; i++) {
            if (enemies[i] is not JsonObject enemy) continue;
            if (!EnemyTargetPriority.IsAlive(enemy)) continue;
            if (EnemyTargetPriority.IsMinion(enemy)) continue;

            var monsterId = enemy["monsterId"]?.GetValue<string>();
            if (MonsterMechanicIndex.TryGet(monsterId, out var profile)
                && profile.Flags.HasFlag(EnemyMechanicFlags.CanSummonAllies))
                return i;
        }

        return -1;
    }

    static string BuildSignature(JsonArray enemies) {
        var parts = new List<string>();
        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i] is not JsonObject e) continue;
            parts.Add($"{e["monsterId"]?.GetValue<string>()}:{e["currentHp"]?.GetValue<int>()}");
        }

        return string.Join("|", parts);
    }
}
