using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Modding;

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
            list.Add(new KitLibModInfo(id, name, ver, CopyDependencies(man)));
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

    static string[] CopyDependencies(ModManifest manifest) {
        var deps = manifest.dependencies;
        if (deps == null || deps.Count == 0)
            return [];

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

        return list.Count == 0 ? [] : list.ToArray();
    }
}

/// <summary>Game-backed mod catalog; safe to call from main thread after mod load.</summary>
public static class ModRuntime {
    public static IModCatalog Catalog => ModCatalog.Default;

    /// <summary>Loaded mods with manifest <c>id</c>, sorted by display name for settings UI lists.</summary>
    public static IReadOnlyList<KitLibModInfo> GetOrderedLoadedMods() {
        var s = Catalog.GetSnapshot();
        if (s.Count <= 1)
            return s;
        var list = s.ToList();
        list.Sort(static (a, b) =>
            string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    /// <summary>
    /// Queued until all mod initializers finish, then invoked on the same thread immediately before
    /// <c>LocManager.Initialize</c>.
    /// </summary>
    public static void RegisterAfterAllModsLoaded(Action registration)
        => ModLoadCoordinator.Register(registration);
}

/// <summary>Shared coordinator for post-mod-load work (KitLib UI and third-party callbacks).</summary>
internal static class ModLoadCoordinator {
    private static readonly object Sync = new();
    private static readonly List<Action> Queue = [];
    private static bool _phaseDone;

    public static void Register(Action registration) {
        ArgumentNullException.ThrowIfNull(registration);

        lock (Sync) {
            if (_phaseDone) {
                Run(registration);
                return;
            }

            Queue.Add(registration);
        }
    }

    public static void Flush() {
        lock (Sync) {
            if (_phaseDone)
                return;

            _phaseDone = true;
            foreach (var action in Queue)
                Run(action);
            Queue.Clear();
        }
    }

    static void Run(Action registration) {
        try {
            registration();
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"ModLoadCoordinator: {ex.Message}");
        }
    }
}
