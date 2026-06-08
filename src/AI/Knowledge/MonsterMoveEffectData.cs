using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KitLib.AI.Knowledge;

/// <summary>Loads static move effects parsed from official monster handlers.</summary>
internal static class MonsterMoveEffectData {
    const string ResourceName = "KitLib.AI.monster-move-effects.json";

    static JsonObject? _root;
    static readonly object Gate = new();

    public static IReadOnlyList<MonsterMoveEffect> GetEffects(string? monsterId, string? moveId) {
        EnsureLoaded();
        var mid = NormalizeId(monsterId);
        var mov = NormalizeId(moveId);
        if (string.IsNullOrEmpty(mid) || string.IsNullOrEmpty(mov))
            return Array.Empty<MonsterMoveEffect>();

        if (_root?["moves"]?[mid]?[mov] is not JsonArray arr)
            return Array.Empty<MonsterMoveEffect>();

        var effects = new List<MonsterMoveEffect>();
        foreach (var node in arr) {
            if (node is not JsonObject obj) continue;
            var parsed = ParseEffect(obj);
            if (parsed != null)
                effects.Add(parsed);
        }

        return effects;
    }

    static MonsterMoveEffect? ParseEffect(JsonObject obj) {
        var kindRaw = obj["kind"]?.GetValue<string>() ?? "";
        if (!Enum.TryParse<MonsterMoveEffectKind>(kindRaw, ignoreCase: true, out var kind))
            return null;

        return new MonsterMoveEffect(
            kind,
            obj["cardId"]?.GetValue<string>(),
            obj["count"]?.GetValue<int>() ?? 0,
            obj["pile"]?.GetValue<string>() ?? "Discard",
            obj["spawnMonsterId"]?.GetValue<string>(),
            obj["powerId"]?.GetValue<string>(),
            obj["attackDamageMultiplier"]?.GetValue<float>() ?? 1f,
            obj["skillCostPenalty"]?.GetValue<int>() ?? 0,
            obj["attackCostPenalty"]?.GetValue<int>() ?? 0,
            obj["boundCardsPerTurn"]?.GetValue<int>() ?? 0,
            obj["damage"]?.GetValue<int>() ?? 0,
            obj["strengthDelta"]?.GetValue<int>() ?? 0,
            obj["isNonDeterministic"]?.GetValue<bool>() == true);
    }

    static void EnsureLoaded() {
        if (_root != null) return;
        lock (Gate) {
            if (_root != null) return;
            _root = LoadRoot() ?? new JsonObject();
        }
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
