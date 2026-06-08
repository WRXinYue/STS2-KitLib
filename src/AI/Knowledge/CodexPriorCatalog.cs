using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using KitLib.Settings;

namespace KitLib.AI.Knowledge;

/// <summary>Community priors trained from Spire Codex A10 macro samples (embedded JSON).</summary>
public static class CodexPriorCatalog {
    const string ResourceName = "KitLib.AI.codex-priors.json";

    static JsonObject? _root;
    static readonly object Gate = new();

    public static void EnsureLoaded() {
        if (_root != null) return;
        lock (Gate) {
            if (_root != null) return;
            _root = LoadRoot() ?? new JsonObject();
        }
    }

    public static bool IsLoaded => _root != null && _root.Count > 0;

    public static float Weight {
        get {
            var w = SettingsStore.Current.CodexPriorWeight;
            if (w < 0f) return 0f;
            if (w > 2f) return 2f;
            return w;
        }
    }

    public static int GetCardBonus(string? characterId, string? cardId, string? context = "combat_reward") {
        EnsureLoaded();
        if (Weight <= 0f || _root == null) return 0;

        var card = NormalizeCardId(cardId);
        var character = NormalizeCharacterId(characterId);
        var ctx = NormalizeContext(context);
        if (string.IsNullOrEmpty(card) || string.IsNullOrEmpty(character)) return 0;

        var cards = _root["cards"] as JsonObject;
        if (cards?[card] is JsonObject cardNode) {
            if (cardNode[character] is JsonObject charNode && charNode[ctx] is JsonObject ctxNode)
                return ScaleBonus(ctxNode["bonus"]?.GetValue<int>() ?? 0);
            if (cardNode["_codex"] is JsonObject codex)
                return ScaleBonus(codex["bonus"]?.GetValue<int>() ?? 0);
        }
        return 0;
    }

    public static int GetRelicBonus(string? characterId, string? relicId, string? context = "combat_reward") {
        EnsureLoaded();
        if (Weight <= 0f || _root == null) return 0;

        var relic = NormalizeRelicId(relicId);
        var character = NormalizeCharacterId(characterId);
        var ctx = NormalizeContext(context);
        if (string.IsNullOrEmpty(relic) || string.IsNullOrEmpty(character)) return 0;

        if (_root["relics"]?[relic]?[character]?[ctx] is JsonObject node)
            return ScaleBonus(node["bonus"]?.GetValue<int>() ?? 0);
        return 0;
    }

    public static int GetRemoveBonus(string? characterId, string? cardId) {
        EnsureLoaded();
        if (Weight <= 0f || _root == null) return 0;

        var card = NormalizeCardId(cardId);
        var character = NormalizeCharacterId(characterId);
        if (string.IsNullOrEmpty(card) || string.IsNullOrEmpty(character)) return 0;

        if (_root["remove"]?[card]?[character] is JsonObject node)
            return ScaleBonus(node["bonus"]?.GetValue<int>() ?? 0);
        return 0;
    }

    public static int GetSkipThresholdOffset(JsonObject snapshot) {
        EnsureLoaded();
        if (Weight <= 0f || _root == null) return 0;

        var character = NormalizeCharacterId(snapshot["characterId"]?.GetValue<string>());
        if (string.IsNullOrEmpty(character)) return 0;

        var act = snapshot["actIndex"]?.GetValue<int>() ?? 0;
        var deckSize = snapshot["deck"]?.AsArray()?.Count ?? snapshot["finalDeckSize"]?.GetValue<int>() ?? 0;
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        var deckBand = DeckSizeBand(deckSize);
        var hpBand = HpRatioBand(hp, maxHp);
        var key = $"{character}|act{act}|deck_{deckBand}|hp_{hpBand}";

        if (_root["skip"]?[key] is JsonObject node)
            return ScaleBonus(node["threshold_offset"]?.GetValue<int>() ?? 0);
        return 0;
    }

    /// <summary>Preferred rest choice: HEAL, SMITH, LIFT; null if no prior.</summary>
    public static string? GetPreferredRestChoice(JsonObject snapshot) {
        EnsureLoaded();
        if (Weight <= 0f || _root == null) return null;

        var character = NormalizeCharacterId(snapshot["characterId"]?.GetValue<string>());
        if (string.IsNullOrEmpty(character)) return null;

        var act = snapshot["actIndex"]?.GetValue<int>() ?? 0;
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        var key = $"{character}|act{act}|hp_{HpRatioBand(hp, maxHp)}";

        return _root["rest"]?[key]?["preferred"]?.GetValue<string>();
    }

    public static int GetEventOptionBonus(
        string? characterId,
        string? eventId,
        string? optionKey,
        out int sampleN) {
        sampleN = 0;
        EnsureLoaded();
        if (Weight <= 0f || _root == null) return 0;

        var character = NormalizeCharacterId(characterId);
        var opt = EventOptionInfer.NormalizeOptionKey(optionKey);
        if (string.IsNullOrEmpty(character) || string.IsNullOrEmpty(opt)) return 0;

        var events = _root["events"] as JsonObject;
        if (events == null) return 0;

        var evt = EventOptionInfer.NormalizeEventId(eventId);
        if (!string.IsNullOrEmpty(evt)
            && TryReadEventOptionNode(events, evt, opt, character, out var bonus, out sampleN))
            return ScaleBonus(bonus);

        foreach (var alias in new[] { "NEOW", "EVENT.NEOW" }) {
            if (TryReadEventOptionNode(events, alias, opt, character, out bonus, out sampleN))
                return ScaleBonus(bonus);
        }

        return 0;
    }

    static bool TryReadEventOptionNode(
        JsonObject events,
        string eventKey,
        string optionKey,
        string character,
        out int bonus,
        out int sampleN) {
        bonus = 0;
        sampleN = 0;
        if (events[eventKey]?[optionKey]?[character] is not JsonObject node)
            return false;
        bonus = node["bonus"]?.GetValue<int>() ?? 0;
        sampleN = node["n"]?.GetValue<int>() ?? 0;
        return true;
    }

    static int ScaleBonus(int raw) => (int)Math.Round(raw * Weight);

    static JsonObject? LoadRoot() {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream == null) {
            MainFile.Logger.Info("[AiPrior] codex-priors.json not embedded; priors disabled.");
            return null;
        }

        try {
            using var reader = new StreamReader(stream);
            var node = JsonNode.Parse(reader.ReadToEnd());
            if (node is JsonObject obj) {
                MainFile.Logger.Info($"[AiPrior] Loaded codex priors v{obj["version"]?.GetValue<int>() ?? 0}.");
                return obj;
            }
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[AiPrior] Failed to load priors: {ex.Message}");
        }
        return null;
    }

    static string? NormalizeCharacterId(string? id) {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var text = id.Trim().ToUpperInvariant();
        const string prefix = "CHARACTER.";
        if (text.StartsWith(prefix, StringComparison.Ordinal))
            text = text[prefix.Length..];
        return text;
    }

    static string? NormalizeCardId(string? id) {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var text = id.Trim().ToUpperInvariant();
        const string prefix = "CARD.";
        if (text.StartsWith(prefix, StringComparison.Ordinal))
            text = text[prefix.Length..];
        return text;
    }

    static string? NormalizeRelicId(string? id) {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var text = id.Trim().ToUpperInvariant();
        const string prefix = "RELIC.";
        if (text.StartsWith(prefix, StringComparison.Ordinal))
            text = text[prefix.Length..];
        return text;
    }

    static string NormalizeContext(string? context) {
        if (string.IsNullOrWhiteSpace(context)) return "combat_reward";
        return context.Trim().ToLowerInvariant() switch {
            "shop" => "shop",
            "event" => "event",
            _ => "combat_reward",
        };
    }

    static string HpRatioBand(int hp, int maxHp) {
        if (maxHp <= 0) return "unknown";
        var ratio = (float)hp / maxHp;
        if (ratio < 0.45f) return "low";
        if (ratio < 0.65f) return "mid";
        if (ratio < 0.85f) return "high";
        return "full";
    }

    static string DeckSizeBand(int deckSize) {
        if (deckSize <= 15) return "small";
        if (deckSize <= 22) return "medium";
        return "large";
    }
}
