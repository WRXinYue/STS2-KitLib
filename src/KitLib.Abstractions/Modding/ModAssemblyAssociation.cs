using System.Collections;
using System.Reflection;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Abstractions.Modding;

/// <summary>
/// Associates extra mod assemblies with the game's <see cref="Mod"/> record.
/// Uses reflection because <c>ModManager.AssociateAssemblyWithMod</c> is not public on all pinned refs.
/// </summary>
public static class ModAssemblyAssociation {
    static readonly MethodInfo? AssociateAssemblyWithModMethod = CreateAssociateAssemblyWithModMethod();

    public static void Associate(string modId, Assembly assembly) {
        if (AssociateAssemblyWithModMethod is not null) {
            try {
                AssociateAssemblyWithModMethod.Invoke(null, [modId, assembly]);
                if (IsAssemblyAssociatedWithMod(modId, assembly))
                    return;
            }
            catch {
                // Fall through to direct mod.assemblies mutation.
            }
        }

        TryAssociateAssemblyWithModList(modId, assembly);
    }

    static bool IsAssemblyAssociatedWithMod(string modId, Assembly assembly) =>
        TryFindMod(modId, out var mod) &&
        TryGetMutableAssembliesList(mod, out var assemblies) &&
        ContainsAssembly(assemblies, assembly);

    static bool TryAssociateAssemblyWithModList(string modId, Assembly assembly) {
        if (!TryFindMod(modId, out var mod))
            return false;

        if (!TryGetMutableAssembliesList(mod, out var assemblies))
            return false;

        if (!ContainsAssembly(assemblies, assembly))
            assemblies.Add(assembly);

        return true;
    }

    static bool TryFindMod(string modId, out Mod mod) {
        foreach (var candidate in ModManager.Mods) {
            if (!string.Equals(ReadManifestId(candidate), modId, StringComparison.Ordinal))
                continue;

            mod = candidate;
            return true;
        }

        mod = null!;
        return false;
    }

    static string? ReadManifestId(Mod mod) {
        var manifest = typeof(Mod)
            .GetField("manifest", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(mod);
        return manifest?.GetType()
            .GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(manifest) as string;
    }

    static bool TryGetMutableAssembliesList(Mod mod, out IList assemblies) {
        assemblies = null!;
        var value = typeof(Mod).GetField("assemblies",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(mod);
        if (value is not IList list)
            return false;

        assemblies = list;
        return true;
    }

    static bool ContainsAssembly(IEnumerable assemblies, Assembly assembly) =>
        assemblies.Cast<object?>().Any(item => ReferenceEquals(item, assembly));

    static MethodInfo? CreateAssociateAssemblyWithModMethod() =>
        typeof(ModManager).GetMethod(
            "AssociateAssemblyWithMod",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(string), typeof(Assembly)],
            null);
}
