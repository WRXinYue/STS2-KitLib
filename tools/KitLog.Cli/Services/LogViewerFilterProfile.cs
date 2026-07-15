using System.Text.Json;
using System.Text.Json.Serialization;
using KitLib.Logging;

namespace KitLog.Cli.Services;

internal sealed class LogViewerFilterProfile {
    public int Version { get; set; } = 1;
    public string? MinLevel { get; set; }
    public string TextFilter { get; set; } = "";
    public SuppressRuleEntry[] SuppressRules { get; set; } = [];
    public string[] HiddenSources { get; set; } = [];
    public string[] LoadedModIds { get; set; } = [];
    public Dictionary<string, string> ModIdAliases { get; set; } = new(StringComparer.Ordinal);

    internal sealed class SuppressRuleEntry {
        public string Pattern { get; set; } = "";
        public bool Enabled { get; set; } = true;
    }
}

internal sealed class LogViewerFilterState {
    public ParsedLogLevel? MinimumLevel { get; init; }
    public string TextFilter { get; init; } = "";
    public IReadOnlyList<(string Pattern, bool Enabled)> SuppressRules { get; init; } = [];
    public HashSet<string> HiddenSources { get; init; } = new(StringComparer.Ordinal);
    public HashSet<string> LoadedModIds { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> ModIdAliases { get; init; } = new(StringComparer.Ordinal);

    public static LogViewerFilterState FromProfile(LogViewerFilterProfile profile) {
        ParsedLogLevel? minLevel = profile.MinLevel?.ToLowerInvariant() switch {
            null or "" => null,
            "info" => ParsedLogLevel.Info,
            "warn" or "warning" => ParsedLogLevel.Warn,
            "error" => ParsedLogLevel.Error,
            _ => null,
        };

        return new LogViewerFilterState {
            MinimumLevel = minLevel,
            TextFilter = profile.TextFilter ?? "",
            SuppressRules = profile.SuppressRules
                .Select(r => (r.Pattern, r.Enabled))
                .ToArray(),
            HiddenSources = profile.HiddenSources.ToHashSet(StringComparer.Ordinal),
            LoadedModIds = profile.LoadedModIds.ToHashSet(StringComparer.Ordinal),
            ModIdAliases = profile.ModIdAliases
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
        };
    }

    public static LogViewerFilterState FromSnapshot(LogViewerFilterSnapshot snapshot) {
        ParsedLogLevel? minLevel = snapshot.MinLevel?.ToLowerInvariant() switch {
            null or "" => null,
            "info" => ParsedLogLevel.Info,
            "warn" or "warning" => ParsedLogLevel.Warn,
            "error" => ParsedLogLevel.Error,
            _ => null,
        };

        return new LogViewerFilterState {
            MinimumLevel = minLevel,
            TextFilter = snapshot.TextFilter ?? "",
            SuppressRules = snapshot.SuppressRules
                .Select(r => (r.Pattern, r.Enabled))
                .ToArray(),
            HiddenSources = snapshot.HiddenSources.ToHashSet(StringComparer.Ordinal),
            LoadedModIds = snapshot.LoadedModIds.ToHashSet(StringComparer.Ordinal),
            ModIdAliases = snapshot.ModIdAliases
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
        };
    }

    public static LogViewerFilterState Defaults() => new() {
        SuppressRules = DefaultSuppressPatterns.Select(p => (p, true)).ToArray(),
    };

    static readonly string[] DefaultSuppressPatterns =
    {
        "AtlasResourceLoader: Missing sprite",
        "Asset not cached:",
        "[Assets] Missing resource path",
        "Found mod manifest file",
        "missing the 'id' field",
        "warmup job failed",
        "Limiting background FPS",
        "Restored foreground FPS",
    };
}

internal sealed class LogViewerFilterWatcher {
    readonly object _lock = new();
    LogViewerFilterState _state = LogViewerFilterState.Defaults();

    public LogViewerFilterState Current {
        get {
            lock (_lock)
                return _state;
        }
    }

    public void ApplyFromSnapshot(LogViewerFilterSnapshot snapshot) {
        lock (_lock)
            _state = LogViewerFilterState.FromSnapshot(snapshot);
    }

    public bool PollForChanges(out bool changed) {
        changed = false;
        return false;
    }
}
