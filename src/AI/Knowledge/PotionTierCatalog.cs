using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KitLib.AI.Knowledge;

/// <summary>Static retain scores for potions (interim until Codex potion priors).</summary>
public static class PotionTierCatalog {
    const string ResourceName = "KitLib.AI.potion-tiers.json";
    const int DefaultScore = 10;

    static JsonObject? _root;
    static readonly object Gate = new();

    public static void EnsureLoaded() {
        if (_root != null) return;
        lock (Gate) {
            if (_root != null) return;
            _root = LoadRoot() ?? new JsonObject();
        }
    }

    public static int GetRetainScore(string? potionId) {
        EnsureLoaded();
        var id = NormalizeId(potionId);
        if (string.IsNullOrEmpty(id)) return DefaultScore;

        if (_root?["potions"]?[id] is JsonValue node)
            return node.GetValue<int>();

        return _root?["default"]?.GetValue<int>() ?? DefaultScore;
    }

    static JsonObject? LoadRoot() {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(ResourceName);
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return JsonNode.Parse(reader.ReadToEnd())?.AsObject();
    }

    static string NormalizeId(string? id) {
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
