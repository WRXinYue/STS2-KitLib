using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KitLib.AI.Knowledge;

/// <summary>Hand-maintained corrections for monster probe edge cases.</summary>
internal static class MonsterProbeOverrides {
    const string ResourceName = "KitLib.AI.monster-probe-overrides.json";

    static JsonObject? _root;
    static readonly object Gate = new();

    static void EnsureLoaded() {
        if (_root != null) return;
        lock (Gate) {
            if (_root != null) return;
            _root = LoadRoot() ?? new JsonObject();
        }
    }

    public static EnemyMechanicFlags GetExtraFlags(string? monsterId) {
        EnsureLoaded();
        var id = NormalizeId(monsterId);
        if (string.IsNullOrEmpty(id)) return EnemyMechanicFlags.None;

        if (_root?["monsters"]?[id]?["addFlags"] is not JsonArray arr)
            return EnemyMechanicFlags.None;

        var flags = EnemyMechanicFlags.None;
        foreach (var node in arr) {
            if (node?.GetValue<string>() is not { } name) continue;
            if (Enum.TryParse<EnemyMechanicFlags>(name, ignoreCase: true, out var flag))
                flags |= flag;
        }

        return flags;
    }

    public static int GetDeathSpawnCount(string? monsterId) {
        EnsureLoaded();
        var id = NormalizeId(monsterId);
        if (string.IsNullOrEmpty(id))
            return 1;

        if (_root?["monsters"]?[id]?["deathSpawnCount"]?.GetValue<int>() is int count && count > 0)
            return count;

        return 1;
    }

    public static IReadOnlyList<string> GetSpawnedIds(string? monsterId) {
        EnsureLoaded();
        var id = NormalizeId(monsterId);
        if (string.IsNullOrEmpty(id)) return Array.Empty<string>();

        if (_root?["monsters"]?[id]?["spawnedMonsterIds"] is not JsonArray arr)
            return Array.Empty<string>();

        var ids = new List<string>();
        foreach (var node in arr) {
            if (node?.GetValue<string>() is { } spawnId && !string.IsNullOrWhiteSpace(spawnId))
                ids.Add(spawnId.Trim().ToUpperInvariant());
        }

        return ids;
    }

    static JsonObject? LoadRoot() {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(ResourceName);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return JsonNode.Parse(reader.ReadToEnd())?.AsObject();
    }

    static string NormalizeId(string? id) =>
        string.IsNullOrWhiteSpace(id) ? "" : id.Trim().ToUpperInvariant();
}
