namespace KitLib.Logging;

/// <summary>
/// Shared filter helpers for log tooling (in-game viewer, kitlog, custom subscribers).
/// Keep behavior aligned with <c>tools/dev-viewer/src/lib/log-filter-state.ts</c>.
/// </summary>
public static class LogStreamFilters {
    public const string HostModSource = "KitLib";
    public const string GameSource = "Game";

    /// <summary>Built-in noise patterns — keep in sync with LogSuppressor / dev-viewer builtins.</summary>
    public static IReadOnlyList<LogViewerFilterSnapshot.SuppressRule> BuiltinSuppressRules { get; } = [
        new() { Pattern = "AtlasResourceLoader: Missing sprite", Enabled = true },
        new() { Pattern = "Asset not cached:", Enabled = true },
        new() { Pattern = "[Assets] Missing resource path", Enabled = true },
        new() { Pattern = "Found mod manifest file", Enabled = true },
        new() { Pattern = "missing the 'id' field", Enabled = true },
        new() { Pattern = "warmup job failed", Enabled = true },
        new() { Pattern = "Limiting background FPS", Enabled = true },
        new() { Pattern = "Restored foreground FPS", Enabled = true },
        new() { Pattern = "The InputMap action", Enabled = true },
    ];

    public static LogViewerFilterSnapshot CreateDefaultFilter() => new() {
        MinLevel = null,
        TextFilter = "",
        HiddenSources = [],
        LoadedModIds = [],
        ModIdAliases = new Dictionary<string, string>(StringComparer.Ordinal),
        SuppressRules = BuiltinSuppressRules.Select(r => new LogViewerFilterSnapshot.SuppressRule {
            Pattern = r.Pattern,
            Enabled = r.Enabled,
        }).ToArray(),
    };

    public static int LevelSeverity(string? lvl) => (lvl ?? "").Trim().ToLowerInvariant() switch {
        "error" => 4,
        "warn" or "warning" => 3,
        "info" or "load" => 2,
        "debug" or "dbg" => 1,
        "vdb" or "verydebug" => 0,
        _ => 1,
    };

    public static bool MeetsMinLevel(string entryLvl, string? minLevel) {
        if (string.IsNullOrWhiteSpace(minLevel))
            return true;
        var minSev = minLevel.Trim().ToLowerInvariant() switch {
            "info" => 2,
            "warn" or "warning" => 3,
            "error" => 4,
            _ => 0,
        };
        return LevelSeverity(entryLvl) >= minSev;
    }

    public static bool IsSessionBoundary(LogStreamEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Boundary || entry.IsFilterFrame)
            return entry.Boundary;
        return KitLogMarkers.ContainsAnySessionBoundary(entry.Text)
               || entry.Text.Contains("[pid=", StringComparison.Ordinal);
    }

    public static bool IsSuppressedByRules(
        string text,
        IReadOnlyList<LogViewerFilterSnapshot.SuppressRule>? rules,
        IDictionary<string, int>? hitCounts = null) {
        if (rules == null || rules.Count == 0)
            return false;
        foreach (var rule in rules) {
            if (!rule.Enabled || string.IsNullOrWhiteSpace(rule.Pattern))
                continue;
            if (!text.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase))
                continue;
            if (hitCounts != null) {
                hitCounts.TryGetValue(rule.Pattern, out var n);
                hitCounts[rule.Pattern] = n + 1;
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Resolve a display source id for filtering (mod id, <see cref="HostModSource"/>, or <see cref="GameSource"/>).
    /// </summary>
    public static string ParseSource(LogStreamEntry entry, LogViewerFilterSnapshot? filter) {
        ArgumentNullException.ThrowIfNull(entry);
        if (!string.IsNullOrWhiteSpace(entry.Mod))
            return entry.Mod.Trim();

        var loaded = filter?.LoadedModIds ?? [];
        if (loaded.Length == 0)
            return GameSource;

        var aliases = filter?.ModIdAliases ?? new Dictionary<string, string>(StringComparer.Ordinal);
        return LogStreamSourceParser.TryFindModId(entry.Text, loaded, aliases) ?? GameSource;
    }

    public static bool IsSourceVisible(string source, LogViewerFilterSnapshot? filter) {
        if (filter?.HiddenSources is not { Length: > 0 } hidden)
            return true;
        return !hidden.Contains(source, StringComparer.Ordinal);
    }

    /// <param name="aiPreset">When true, only lines mentioning AutoPlay / AiHost / companion tags pass.</param>
    public static bool ShouldShow(
        LogStreamEntry entry,
        LogViewerFilterSnapshot? filter,
        bool aiPreset = false,
        IDictionary<string, int>? suppressHits = null) {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.IsFilterFrame)
            return false;

        if (aiPreset && !ContainsAiTag(entry.Text))
            return false;

        if (IsSessionBoundary(entry))
            return true;

        if (!MeetsMinLevel(entry.Lvl, filter?.MinLevel))
            return false;

        var textFilter = filter?.TextFilter?.Trim();
        if (!string.IsNullOrEmpty(textFilter)
            && !entry.Text.Contains(textFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (IsSuppressedByRules(entry.Text, filter?.SuppressRules, suppressHits))
            return false;

        var source = ParseSource(entry, filter);
        return IsSourceVisible(source, filter);
    }

    /// <summary>Filter a batch; skips filter frames; keeps session boundaries when they pass <see cref="ShouldShow"/>.</summary>
    public static IReadOnlyList<LogStreamEntry> WhereVisible(
        IEnumerable<LogStreamEntry> entries,
        LogViewerFilterSnapshot? filter,
        bool aiPreset = false) {
        ArgumentNullException.ThrowIfNull(entries);
        var list = new List<LogStreamEntry>();
        foreach (var entry in entries) {
            if (ShouldShow(entry, filter, aiPreset))
                list.Add(entry);
        }
        return list;
    }

    static bool ContainsAiTag(string text)
        => text.Contains("[AutoPlay", StringComparison.Ordinal)
           || text.Contains("[AiHost", StringComparison.Ordinal)
           || text.Contains("[MpAi", StringComparison.Ordinal)
           || text.Contains("[LanLocal", StringComparison.Ordinal)
           || text.Contains("[Companion", StringComparison.Ordinal);
}
