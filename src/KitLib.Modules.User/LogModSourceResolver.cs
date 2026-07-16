using System.Collections.Generic;
using KitLib.Modding;

namespace KitLib;

/// <summary>Shared mod id / display-name alias map for log source attribution and filter sync.</summary>
internal static class LogModSourceResolver {
    public static Dictionary<string, string> BuildAliasLookup(HashSet<string> loadedModIds) {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var mod in ModRuntime.Catalog.GetSnapshot()) {
            RegisterAlias(map, mod.Id, mod.Id);
            if (!string.IsNullOrEmpty(mod.DisplayName))
                RegisterAlias(map, mod.DisplayName, mod.Id);
        }

        foreach (var id in loadedModIds)
            RegisterAlias(map, id, id);

        return map;
    }

    public static string NormalizeIdKey(string id)
        => id.ToLowerInvariant().Replace('-', '_');

    static void RegisterAlias(Dictionary<string, string> map, string alias, string canonicalId) {
        var key = NormalizeIdKey(alias);
        if (!map.ContainsKey(key))
            map[key] = canonicalId;
    }
}
