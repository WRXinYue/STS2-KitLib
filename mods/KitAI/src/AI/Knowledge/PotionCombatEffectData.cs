using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KitLib.AI.Knowledge;

/// <summary>Loads combat potion effect profiles for beam simulation.</summary>
internal static class PotionCombatEffectData {
    const string ResourceName = "KitLib.AI.potion-combat-effects.json";

    static JsonObject? _root;
    static readonly object Gate = new();

    public static bool TryGetProfile(string? potionId, out PotionCombatProfile profile) {
        EnsureLoaded();
        profile = null!;
        var id = NormalizeId(potionId);
        if (string.IsNullOrEmpty(id))
            return false;

        if (_root?["potions"]?[id] is not JsonObject obj)
            return false;

        profile = ParseProfile(id, obj);
        return true;
    }

    public static bool IsSimulatable(string? potionId) =>
        TryGetProfile(potionId, out var profile) && profile.Simulatable;

    static PotionCombatProfile ParseProfile(string id, JsonObject obj) {
        var effects = new List<PotionCombatEffect>();
        if (obj["effects"] is JsonArray effectArr) {
            foreach (var node in effectArr) {
                if (node is not JsonObject effectObj) continue;
                var parsed = ParseEffect(effectObj);
                if (parsed != null)
                    effects.Add(parsed);
            }
        }

        PotionRandomSpec? random = null;
        if (obj["random"] is JsonObject randomObj)
            random = ParseRandom(randomObj);

        return new PotionCombatProfile(
            id,
            obj["targetType"]?.GetValue<string>() ?? "",
            effects,
            random,
            obj["simulatable"]?.GetValue<bool>() == true);
    }

    static PotionCombatEffect? ParseEffect(JsonObject obj) {
        var kindRaw = obj["kind"]?.GetValue<string>() ?? "";
        if (!Enum.TryParse<PotionCombatEffectKind>(kindRaw, ignoreCase: true, out var kind))
            return null;

        return new PotionCombatEffect(
            kind,
            obj["amount"]?.GetValue<int>() ?? 0,
            obj["target"]?.GetValue<string>());
    }

    static PotionRandomSpec ParseRandom(JsonObject obj) =>
        new(
            obj["kind"]?.GetValue<string>() ?? "AddCardsFromPool",
            obj["pool"]?.GetValue<string>() ?? "",
            obj["pickCount"]?.GetValue<int>() ?? 1,
            obj["offerCount"]?.GetValue<int>() ?? 3,
            obj["mcSamples"]?.GetValue<int>() ?? 3);

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

    public static string NormalizeId(string? id) {
        if (string.IsNullOrWhiteSpace(id)) return "";
        var s = id.Trim();
        if (s.StartsWith("POTION.", StringComparison.OrdinalIgnoreCase))
            s = s["POTION.".Length..];
        return s.ToUpperInvariant();
    }

    internal static void ClearForTests() {
        _root = null;
    }
}
