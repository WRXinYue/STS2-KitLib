using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KitLib.AI.Knowledge;

/// <summary>Loads combat relic profiles extracted from official RelicModel handlers.</summary>
internal static class RelicCombatEffectData {
    const string ResourceName = "KitLib.AI.relic-combat-effects.json";

    static JsonObject? _root;
    static readonly object Gate = new();

    public static bool TryGetProfile(string? relicId, out RelicCombatProfile profile) {
        EnsureLoaded();
        profile = null!;
        var id = NormalizeId(relicId);
        if (string.IsNullOrEmpty(id))
            return false;

        if (_root?["relics"]?[id] is not JsonObject obj)
            return false;

        profile = ParseProfile(id, obj);
        return true;
    }

    public static IReadOnlyList<RelicCombatProfile> GetProfiles(IEnumerable<string> relicIds) {
        var profiles = new List<RelicCombatProfile>();
        foreach (var relicId in relicIds) {
            if (TryGetProfile(relicId, out var profile))
                profiles.Add(profile);
        }

        return profiles;
    }

    static RelicCombatProfile ParseProfile(string id, JsonObject obj) {
        var hooks = new List<string>();
        if (obj["hooks"] is JsonArray hookArr) {
            foreach (var node in hookArr) {
                if (node?.GetValue<string>() is { } hook && !string.IsNullOrWhiteSpace(hook))
                    hooks.Add(hook);
            }
        }

        var effects = new List<RelicCombatEffect>();
        if (obj["effects"] is JsonArray effectArr) {
            foreach (var node in effectArr) {
                if (node is not JsonObject effectObj) continue;
                var parsed = ParseEffect(effectObj);
                if (parsed != null)
                    effects.Add(parsed);
            }
        }

        return new RelicCombatProfile(
            id,
            hooks,
            effects,
            obj["simulatable"]?.GetValue<bool>() == true,
            obj["needsManual"]?.GetValue<bool>() == true);
    }

    static RelicCombatEffect? ParseEffect(JsonObject obj) {
        var kindRaw = obj["kind"]?.GetValue<string>() ?? "";
        if (!Enum.TryParse<RelicCombatEffectKind>(kindRaw, ignoreCase: true, out var kind))
            return null;

        return new RelicCombatEffect(
            kind,
            obj["delta"]?.GetValue<int>() ?? 0,
            obj["count"]?.GetValue<int>() ?? 0,
            obj["block"]?.GetValue<int>() ?? 0,
            obj["powerId"]?.GetValue<string>(),
            obj["amount"]?.GetValue<int>() ?? 1,
            obj["maxCombatRound"]?.GetValue<int>(),
            obj["minCombatRound"]?.GetValue<int>());
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
