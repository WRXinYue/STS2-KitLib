using System.Collections;
using System.Reflection;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib;

internal static class Sts2ManifestCompat {
    public static string[] CopyDependencies(ModManifest manifest) {
        var deps = GetDependenciesCollection(manifest);
        if (deps == null)
            return [];

        return CopyStructuredDependencies(deps);
    }

    static IEnumerable? GetDependenciesCollection(ModManifest manifest) {
        var prop = manifest.GetType().GetProperty(
            "dependencies",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(manifest) as IEnumerable;
    }

    static string[] CopyStructuredDependencies(IEnumerable deps) {
        var list = new List<string>();
        foreach (var dep in deps) {
            if (dep == null)
                continue;
            if (dep is string s) {
                if (!string.IsNullOrEmpty(s))
                    list.Add(s);
                continue;
            }

            var id = GetMemberString(dep, "id");
            if (string.IsNullOrEmpty(id))
                continue;
            var minVersion = GetMemberString(dep, "minVersion");
            list.Add(string.IsNullOrEmpty(minVersion) ? id : $"{id}>={minVersion}");
        }
        return list.Count == 0 ? [] : list.ToArray();
    }

    static string? GetMemberString(object obj, string name) {
        var t = obj.GetType();
        var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop?.GetValue(obj) is string s && !string.IsNullOrWhiteSpace(s))
            return s;
        var field = t.GetField(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field?.GetValue(obj) is string s2 && !string.IsNullOrWhiteSpace(s2))
            return s2;
        return null;
    }
}
