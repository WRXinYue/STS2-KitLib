using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitLib.Abstractions.Logging;
using KitLib.Modding;
using MegaCrit.Sts2.Core.Logging;

namespace KitLib;

/// <summary>
/// Mirrors in-game log viewer filters to <c>instances/{pid}/log-viewer-filter.json</c>
/// so <c>kitlog --sync-viewer</c> stays in sync (including live suppress-rule toggles).
/// </summary>
internal static class LogViewerFilterSync {
    static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void PublishDefaults() {
        var loaded = ModRuntime.Catalog.GetIdSet();
        Publish(null, "", BuildDefaultModVisibility(loaded), loaded, BuildModIdAliasLookup(loaded));
    }

    public static void Publish(
        LogLevel? minLevel,
        string textFilter,
        IReadOnlyDictionary<string, bool> modVisible,
        HashSet<string> loadedModIds,
        Dictionary<string, string> modIdAliases) {
        try {
            var path = KitLibSession.GetFilterProfilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var profile = new FilterProfileDto {
                Version = LogViewerFilterContract.Version,
                MinLevel = ToMinLevelToken(minLevel),
                TextFilter = textFilter ?? "",
                SuppressRules = LogSuppressor.BuiltInRules
                    .Select(r => new SuppressRuleDto { Pattern = r.Pattern, Enabled = r.Enabled })
                    .ToArray(),
                HiddenSources = modVisible
                    .Where(kv => !kv.Value)
                    .Select(kv => kv.Key)
                    .OrderBy(k => k, StringComparer.Ordinal)
                    .ToArray(),
                LoadedModIds = loadedModIds
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                ModIdAliases = modIdAliases
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
            };

            File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
        }
        catch (Exception ex) {
            KitLog.Warn("LogViewer", $"Failed to sync kitlog filters: {ex.Message}");
        }
    }

    static Dictionary<string, bool> BuildDefaultModVisibility(HashSet<string> loadedModIds) {
        var map = new Dictionary<string, bool>(StringComparer.Ordinal) { ["Game"] = true };
        foreach (var id in loadedModIds)
            map[id] = true;
        return map;
    }

    static Dictionary<string, string> BuildModIdAliasLookup(HashSet<string> loadedModIds) {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var mod in ModRuntime.Catalog.GetSnapshot()) {
            RegisterModAlias(map, mod.Id, mod.Id);
            if (!string.IsNullOrEmpty(mod.DisplayName))
                RegisterModAlias(map, mod.DisplayName, mod.Id);
        }

        foreach (var id in loadedModIds)
            RegisterModAlias(map, id, id);

        return map;
    }

    static void RegisterModAlias(Dictionary<string, string> map, string alias, string canonicalId) {
        var key = NormalizeModIdKey(alias);
        if (!map.ContainsKey(key))
            map[key] = canonicalId;
    }

    static string NormalizeModIdKey(string id)
        => id.ToLowerInvariant().Replace('-', '_');

    static string? ToMinLevelToken(LogLevel? minLevel) => minLevel switch {
        null => null,
        LogLevel.Info => "info",
        LogLevel.Warn => "warn",
        LogLevel.Error => "error",
        _ => null,
    };

    sealed class FilterProfileDto {
        public int Version { get; set; }
        public string? MinLevel { get; set; }
        public string TextFilter { get; set; } = "";
        public SuppressRuleDto[] SuppressRules { get; set; } = [];
        public string[] HiddenSources { get; set; } = [];
        public string[] LoadedModIds { get; set; } = [];
        public Dictionary<string, string> ModIdAliases { get; set; } = new(StringComparer.Ordinal);
    }

    sealed class SuppressRuleDto {
        public string Pattern { get; set; } = "";
        public bool Enabled { get; set; }
    }
}
