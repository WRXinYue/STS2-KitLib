namespace KitLog.Cli.Services;

internal static class LogViewerFilterMatcher {
    public static bool IsSessionBoundary(string line)
        => KitLogMarkers.ContainsAnySessionBoundary(line);

    public static bool ShouldShow(
        string line,
        ParsedLogLine parsed,
        LogViewerFilterState state,
        bool applyViewerFilters) {
        if (IsSessionBoundary(line))
            return true;

        if (!applyViewerFilters)
            return true;

        if (state.MinimumLevel is ParsedLogLevel min
            && !LogLineParser.MeetsMinimumLevel(parsed.Level, min))
            return false;

        if (!string.IsNullOrWhiteSpace(state.TextFilter)
            && !line.Contains(state.TextFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (IsSuppressed(line, state.SuppressRules))
            return false;

        var source = ParseSource(line, state.LoadedModIds, state.ModIdAliases);
        if (state.HiddenSources.Contains(source))
            return false;

        return true;
    }

    static bool IsSuppressed(string text, IReadOnlyList<(string Pattern, bool Enabled)> rules) {
        foreach (var (pattern, enabled) in rules) {
            if (!enabled || string.IsNullOrEmpty(pattern))
                continue;
            if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static string ParseSource(
        string text,
        HashSet<string> loadedModIds,
        Dictionary<string, string> modIdAliases) {
        if (loadedModIds.Count == 0 || string.IsNullOrEmpty(text))
            return "Game";

        return TryFindModTag(text, loadedModIds, modIdAliases, out var modId)
            ? modId
            : "Game";
    }

    public static bool TryFindModTagSpan(
        string text,
        HashSet<string> loadedModIds,
        Dictionary<string, string> modIdAliases,
        out int tagStart,
        out int tagEndExclusive,
        out string modId) {
        tagStart = 0;
        tagEndExclusive = 0;
        modId = "";
        int i = 0;
        while (i < text.Length) {
            int open = text.IndexOf('[', i);
            if (open < 0)
                break;

            int close = text.IndexOf(']', open + 1);
            if (close <= open + 1) {
                i = open + 1;
                continue;
            }

            string inner = text.Substring(open + 1, close - open - 1);
            if (TryResolveModId(inner, loadedModIds, modIdAliases, out modId)) {
                tagStart = open;
                tagEndExclusive = close + 1;
                return true;
            }

            i = close + 1;
        }

        return false;
    }

    static bool TryFindModTag(
        string text,
        HashSet<string> loadedModIds,
        Dictionary<string, string> modIdAliases,
        out string modId)
        => TryFindModTagSpan(text, loadedModIds, modIdAliases, out _, out _, out modId);

    /// <summary>First non-level <c>[tag]</c> segment; used to color KitLib and other framework tags in session.log.</summary>
    public static bool TryFindAnyModTagSpan(
        string text,
        out int tagStart,
        out int tagEndExclusive,
        out string tagInner) {
        tagStart = 0;
        tagEndExclusive = 0;
        tagInner = "";

        int i = 0;
        while (i < text.Length) {
            int open = text.IndexOf('[', i);
            if (open < 0)
                break;

            int close = text.IndexOf(']', open + 1);
            if (close <= open + 1) {
                i = open + 1;
                continue;
            }

            var inner = text.Substring(open + 1, close - open - 1);
            if (!LogLevelBracketTags.Contains(inner)) {
                tagStart = open;
                tagEndExclusive = close + 1;
                tagInner = inner;
                return true;
            }

            i = close + 1;
        }

        return false;
    }

    static readonly HashSet<string> LogLevelBracketTags = new(StringComparer.OrdinalIgnoreCase) {
        "INFO", "WARN", "WARNING", "ERROR", "DEBUG", "LOAD", "VERYDEBUG", "VDB", "DBG",
    };

    static bool TryResolveModId(
        string candidate,
        HashSet<string> loadedModIds,
        Dictionary<string, string> modIdAliases,
        out string modId) {
        modId = "";

        if (LogLevelBracketTags.Contains(candidate))
            return false;

        if (loadedModIds.Contains(candidate)) {
            modId = candidate;
            return true;
        }

        if (TryResolveModIdKey(NormalizeModIdKey(candidate), modIdAliases, out modId))
            return true;

        int lastDot = candidate.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < candidate.Length - 1 &&
            TryResolveModIdKey(NormalizeModIdKey(candidate[(lastDot + 1)..]), modIdAliases, out modId))
            return true;

        return false;
    }

    static bool TryResolveModIdKey(string normalizedKey, Dictionary<string, string> modIdAliases, out string modId) {
        if (modIdAliases.TryGetValue(normalizedKey, out var resolved) && resolved != null) {
            modId = resolved;
            return true;
        }

        modId = "";
        return false;
    }

    static string NormalizeModIdKey(string id)
        => id.ToLowerInvariant().Replace('-', '_');
}
