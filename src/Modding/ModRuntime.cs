using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Modding;

/// <summary>Stable snapshot of one loaded mod (manifest-backed).</summary>
public readonly record struct KitLibModInfo(
    string Id,
    string DisplayName,
    string Version,
    IReadOnlyList<string> Dependencies);

/// <summary>Read-only view of mods the game has already scanned and loaded.</summary>
public interface IModCatalog {
    /// <summary>Copies current loaded-mod entries that have a non-empty manifest <c>id</c>.</summary>
    IReadOnlyList<KitLibModInfo> GetSnapshot();

    /// <summary>Fast membership checks (e.g. log line attribution). Empty if no mods loaded.</summary>
    HashSet<string> GetIdSet(StringComparer? comparer = null);
}

/// <summary>Default <see cref="IModCatalog"/> backed by the game's loaded mod enumeration.</summary>
public sealed class ModCatalog : IModCatalog {
    public static IModCatalog Default { get; } = new ModCatalog();

    private ModCatalog() { }

    public IReadOnlyList<KitLibModInfo> GetSnapshot() {
        var mods = ModManagerLoadedMods.Enumerate().ToList();
        if (mods.Count == 0)
            return Array.Empty<KitLibModInfo>();

        var list = new List<KitLibModInfo>(mods.Count);
        foreach (var m in mods) {
            var man = m.manifest;
            if (man == null) continue;
            var id = man.id;
            if (string.IsNullOrEmpty(id)) continue;
            var name = string.IsNullOrEmpty(man.name) ? id : man.name;
            var ver = man.version ?? "";
            list.Add(new KitLibModInfo(id, name, ver, ModRuntime.CopyDependencies(man)));
        }

        return list;
    }

    public HashSet<string> GetIdSet(StringComparer? comparer = null) {
        comparer ??= StringComparer.Ordinal;
        var set = new HashSet<string>(comparer);
        foreach (var m in ModManagerLoadedMods.Enumerate()) {
            var id = m.manifest?.id;
            if (!string.IsNullOrEmpty(id))
                set.Add(id);
        }

        return set;
    }
}

/// <summary>Game-backed mod catalog; safe to call from main thread after mod load.</summary>
public static class ModRuntime {
    public static IModCatalog Catalog => ModCatalog.Default;

    internal static string[] CopyDependencies(ModManifest manifest) {
        var deps = manifest.dependencies;
        if (deps == null || deps.Count == 0)
            return Array.Empty<string>();

        var list = new List<string>(deps.Count);
#if STS2_BETA
        foreach (var dep in deps) {
            if (string.IsNullOrEmpty(dep.id))
                continue;
            list.Add(string.IsNullOrEmpty(dep.minVersion) ? dep.id : $"{dep.id}>={dep.minVersion}");
        }
#else
        foreach (var dep in deps) {
            if (!string.IsNullOrEmpty(dep))
                list.Add(dep);
        }
#endif

        return list.Count == 0 ? Array.Empty<string>() : list.ToArray();
    }
}
