using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Logging;
using KitLib.Modding;
using MegaCrit.Sts2.Core.Logging;

namespace KitLib;

/// <summary>
/// Streams in-game log viewer filters over the KitLib log pipe for <c>kitlog attach --sync-viewer</c>.
/// </summary>
internal static class LogViewerFilterSync {
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
            LogStreamHub.PublishFilter(new LogViewerFilterSnapshot {
                MinLevel = ToMinLevelToken(minLevel),
                TextFilter = textFilter ?? "",
                SuppressRules = LogSuppressor.BuiltInRules
                    .Select(r => new LogViewerFilterSnapshot.SuppressRule {
                        Pattern = r.Pattern,
                        Enabled = r.Enabled,
                    })
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
            });
        }
        catch (Exception ex) {
            KitLog.Warn("LogViewer", $"Failed to stream kitlog filters: {ex.Message}");
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
}
