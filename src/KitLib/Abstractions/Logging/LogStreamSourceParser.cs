namespace KitLib.Logging;

/// <summary>Bracket-tag mod id resolution for plain log lines (aligned with dev-viewer log-tag-matcher).</summary>
public static class LogStreamSourceParser {
    static readonly HashSet<string> LevelBracketTags = new(StringComparer.OrdinalIgnoreCase) {
        "info", "warn", "warning", "error", "debug", "load", "verydebug", "vdb", "dbg",
    };

    public static string? TryFindModId(
        string text,
        IReadOnlyList<string> loadedModIds,
        IReadOnlyDictionary<string, string> modIdAliases) {
        if (string.IsNullOrEmpty(text) || loadedModIds.Count == 0)
            return null;

        var loaded = new HashSet<string>(loadedModIds, StringComparer.Ordinal);
        var aliases = NormalizeAliases(modIdAliases);

        var i = 0;
        while (i < text.Length) {
            var open = text.IndexOf('[', i);
            if (open < 0)
                break;
            var close = text.IndexOf(']', open + 1);
            if (close < 0)
                break;

            var inner = text.AsSpan(open + 1, close - open - 1).Trim();
            var candidate = inner.ToString();
            var resolved = TryResolveModId(candidate, loaded, aliases);
            if (resolved != null)
                return resolved;

            i = close + 1;
        }

        return null;
    }

    static string? TryResolveModId(
        string candidate,
        HashSet<string> loadedModIds,
        Dictionary<string, string> aliases) {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;
        if (LevelBracketTags.Contains(candidate))
            return null;
        if (candidate.Contains('=') || candidate.Contains(','))
            return null;
        if (loadedModIds.Contains(candidate))
            return candidate;

        var key = NormalizeIdKey(candidate);
        if (aliases.TryGetValue(key, out var byKey))
            return byKey;

        var lastDot = candidate.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < candidate.Length - 1) {
            var suffixKey = NormalizeIdKey(candidate[(lastDot + 1)..]);
            if (aliases.TryGetValue(suffixKey, out var bySuffix))
                return bySuffix;
        }

        return null;
    }

    static string NormalizeIdKey(string id) => id.ToLowerInvariant().Replace('-', '_');

    static Dictionary<string, string> NormalizeAliases(IReadOnlyDictionary<string, string> modIdAliases) {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in modIdAliases) {
            if (string.IsNullOrWhiteSpace(k) || string.IsNullOrWhiteSpace(v))
                continue;
            map[NormalizeIdKey(k)] = v;
        }
        return map;
    }
}
