using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Knowledge;

/// <summary>Indexes official monster mechanics from encounters at startup.</summary>
public static class MonsterMechanicIndex {
    static readonly Dictionary<string, MonsterMechanicProfile> ById = new(StringComparer.OrdinalIgnoreCase);
    static bool _initialized;

    public static void Initialize() {
        if (_initialized) return;
        _initialized = true;

        var spawnHints = BuildEncounterSpawnHints();

        foreach (var monster in EnemyActions.GetAllMonsters()) {
            try {
                var id = monster.Id.Entry ?? "";
                if (string.IsNullOrWhiteSpace(id)) continue;

                IReadOnlyList<string> hintedSpawns = spawnHints.TryGetValue(id, out var hinted)
                    ? hinted
                    : Array.Empty<string>();
                ById[id] = OfficialMonsterProbe.BuildProfile(monster, hintedSpawns);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[AiMechanic] Skipped monster {monster.Id.Entry}: {ex.Message}");
            }
        }

        MainFile.Logger.Info($"[AiMechanic] MonsterMechanicIndex indexed {ById.Count} monsters.");
    }

    public static bool TryGet(string? id, out MonsterMechanicProfile profile) {
        EnsureInitialized();
        profile = null!;
        if (string.IsNullOrWhiteSpace(id)) return false;
        return ById.TryGetValue(id, out profile!);
    }

    public static MonsterMechanicProfile GetOrDefault(string? id) {
        if (TryGet(id, out var profile))
            return profile;
        return new MonsterMechanicProfile(
            id ?? "",
            EnemyMechanicFlags.None,
            Array.Empty<MonsterMoveProfile>(),
            Array.Empty<string>());
    }

    public static IReadOnlyList<MonsterMechanicProfile> AllProfiles() {
        EnsureInitialized();
        return ById.Values.OrderBy(p => p.MonsterId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static JsonArray ToJsonArray() {
        var arr = new JsonArray();
        foreach (var profile in AllProfiles()) {
            arr.Add(new JsonObject {
                ["monsterId"] = profile.MonsterId,
                ["flags"] = profile.Flags.ToString(),
                ["spawnedMonsterIds"] = new JsonArray(
                    profile.SpawnedMonsterIds.Select(id => JsonValue.Create(id)).ToArray()),
                ["moves"] = SerializeMoves(profile.Moves),
            });
        }

        return arr;
    }

    static JsonArray SerializeMoves(IReadOnlyList<MonsterMoveProfile> moves) {
        var arr = new JsonArray();
        foreach (var move in moves) {
            var effects = new JsonArray();
            foreach (var effect in move.Effects) {
                var obj = new JsonObject {
                    ["kind"] = effect.Kind.ToString(),
                };
                if (!string.IsNullOrWhiteSpace(effect.CardId))
                    obj["cardId"] = effect.CardId;
                if (effect.Count > 0)
                    obj["count"] = effect.Count;
                if (!string.IsNullOrWhiteSpace(effect.Pile))
                    obj["pile"] = effect.Pile;
                if (!string.IsNullOrWhiteSpace(effect.SpawnMonsterId))
                    obj["spawnMonsterId"] = effect.SpawnMonsterId;
                if (!string.IsNullOrWhiteSpace(effect.PowerId))
                    obj["powerId"] = effect.PowerId;
                effects.Add(obj);
            }

            arr.Add(new JsonObject {
                ["moveId"] = move.MoveId,
                ["intentTypes"] = new JsonArray(
                    move.IntentTypes.Select(t => JsonValue.Create(t.ToString())).ToArray()),
                ["effects"] = effects,
            });
        }

        return arr;
    }

    static Dictionary<string, List<string>> BuildEncounterSpawnHints() {
        var hints = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var encounter in ModelDb.AllEncounters) {
            var monsters = (encounter.AllPossibleMonsters ?? Enumerable.Empty<MonsterModel>())
                .Select(m => m.Id.Entry)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (monsters.Count < 2) continue;

            foreach (var primaryId in monsters) {
                if (!hints.TryGetValue(primaryId, out var list)) {
                    list = new List<string>();
                    hints[primaryId] = list;
                }

                foreach (var other in monsters) {
                    if (!string.Equals(primaryId, other, StringComparison.OrdinalIgnoreCase)
                        && !list.Contains(other, StringComparer.OrdinalIgnoreCase))
                        list.Add(other);
                }
            }
        }

        return hints;
    }

    static void EnsureInitialized() {
        if (!_initialized)
            Initialize();
    }
}
