using System;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KitLib.AI.Knowledge;

internal static class EventOptionInfer {
    static readonly (string Hint, string OptionKey)[] TitleHints = {
        ("铅制镇纸", "LEAD_PAPERWEIGHT"),
        ("沉重石碑", "HEFTY_TABLET"),
        ("庞大卷轴", "MASSIVE_SCROLL"),
        ("扭蛋", "RELIC_CAPSULE"),
        ("随机遗物", "RANDOM_RELIC"),
        ("巨大扭蛋", "RELIC_CAPSULE"),
        ("药水位", "POTION_SLOT"),
        ("药水栏", "POTION_SLOT"),
        ("删牌", "REMOVE_CARD"),
        ("移除一张", "REMOVE_CARD"),
        ("变形", "TRANSFORM_CARD"),
        ("升级", "UPGRADE_CARD"),
        ("金币", "GOLD"),
        ("珍珠", "PEARL"),
        ("无色", "COLORLESS_CARD"),
        ("选牌", "CARD_REWARD"),
    };

    static readonly (string Hint, string RelicId)[] RelicTitleHints = {
        ("铅制镇纸", "LEAD_PAPERWEIGHT"),
        ("沉重石碑", "HEFTY_TABLET"),
        ("庞大卷轴", "MASSIVE_SCROLL"),
        ("扭蛋", "NEOWS_LAMENT"),
    };

    static readonly Regex ModelIdPattern = new(
        @"\b([A-Z][A-Z0-9_]{3,})\b",
        RegexOptions.Compiled);

    public static void FillOptionKey(JsonObject opt) {
        if (opt["optionKey"]?.GetValue<string>() is { Length: > 0 } existing) {
            opt["optionKey"] = NormalizeOptionKey(existing);
            return;
        }

        var key = OptionKeyFromFields(opt);
        if (!string.IsNullOrEmpty(key))
            opt["optionKey"] = key;
    }

    public static string? OptionKey(JsonObject opt) {
        var precomputed = opt["optionKey"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(precomputed))
            return NormalizeOptionKey(precomputed);
        return OptionKeyFromFields(opt);
    }

    static string? OptionKeyFromFields(JsonObject opt) {
        var modelId = opt["modelId"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(modelId)) {
            var fromModel = NormalizeOptionKey(modelId);
            if (!string.IsNullOrEmpty(fromModel))
                return fromModel;
        }

        var textKey = opt["textKey"]?.GetValue<string>() ?? "";
        var fromKey = ExtractTokenFromBlob(textKey);
        if (!string.IsNullOrEmpty(fromKey))
            return fromKey;

        var title = opt["title"]?.GetValue<string>() ?? "";
        foreach (var (hint, optionKey) in TitleHints) {
            if (title.Contains(hint, StringComparison.Ordinal))
                return optionKey;
        }

        return ExtractTokenFromBlob($"{textKey} {title}");
    }

    public static string? RelicId(JsonObject opt) {
        var key = OptionKey(opt);
        if (!string.IsNullOrEmpty(key) && LooksLikeRelicId(key))
            return key;

        var title = opt["title"]?.GetValue<string>() ?? "";
        foreach (var (hint, relicId) in RelicTitleHints) {
            if (title.Contains(hint, StringComparison.Ordinal))
                return relicId;
        }

        return ExtractRelicTokenFromBlob($"{opt["textKey"]} {title}");
    }

    static string? ExtractTokenFromBlob(string blob) {
        foreach (Match match in ModelIdPattern.Matches(blob.ToUpperInvariant())) {
            var token = match.Groups[1].Value;
            if (token.StartsWith("NEOW", StringComparison.Ordinal) && token.Length <= 6)
                continue;
            if (token is "OPTION" or "EVENT" or "RELIC" or "CARD" or "CHOICE" or "TITLE")
                continue;
            if (token.Contains('_', StringComparison.Ordinal))
                return token;
        }
        return null;
    }

    static string? ExtractRelicTokenFromBlob(string blob) {
        foreach (Match match in ModelIdPattern.Matches(blob.ToUpperInvariant())) {
            var token = match.Groups[1].Value;
            if (token.StartsWith("NEOW", StringComparison.Ordinal)) continue;
            if (token is "OPTION" or "EVENT" or "RELIC" or "CARD" or "CHOICE") continue;
            if (token.Contains('_', StringComparison.Ordinal) && LooksLikeRelicId(token))
                return token;
        }
        return null;
    }

    static bool LooksLikeRelicId(string token) =>
        token is not ("REMOVE_CARD" or "TRANSFORM_CARD" or "UPGRADE_CARD" or "POTION_SLOT"
            or "RELIC_CAPSULE" or "RANDOM_RELIC" or "CARD_REWARD" or "COLORLESS_CARD" or "GOLD" or "PEARL");

    public static string? NormalizeOptionKey(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim().ToUpperInvariant();
        foreach (var prefix in new[] { "EVENT.OPTION.", "EVENT.", "NEOW.OPTION.", "NEOW.", "OPTION." }) {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
                text = text[prefix.Length..];
        }
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static string? NormalizeEventId(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim().ToUpperInvariant();
        if (text.StartsWith("EVENT.", StringComparison.Ordinal))
            text = text["EVENT.".Length..];
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
