using System.Collections;
using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib;

internal static class VariantAssemblyAssociation {
    static readonly MethodInfo? AssociateAssemblyWithModMethod = typeof(ModManager).GetMethod(
        "AssociateAssemblyWithMod",
        BindingFlags.Public | BindingFlags.Static,
        null,
        [typeof(string), typeof(Assembly)],
        null);

    internal static void Associate(string modId, Assembly assembly) {
        if (AssociateAssemblyWithModMethod != null) {
            try {
                AssociateAssemblyWithModMethod.Invoke(null, [modId, assembly]);
                if (IsAssociated(modId, assembly))
                    return;
            }
            catch (Exception ex) {
                Log.Warn($"[KitLib.Loader] Failed to associate {assembly.FullName} with {modId}: {ex.Message}");
            }
        }

        if (TryAddToModList(modId, assembly))
            return;

        Log.Warn($"[KitLib.Loader] Could not associate {assembly.FullName} with {modId}.");
    }

    static bool IsAssociated(string modId, Assembly assembly) =>
        TryFindMod(modId, out var mod) &&
        TryGetAssemblies(mod, out var assemblies) &&
        Contains(assemblies, assembly);

    static bool TryAddToModList(string modId, Assembly assembly) {
        if (!TryFindMod(modId, out var mod) || !TryGetAssemblies(mod, out var assemblies))
            return false;

        if (!Contains(assemblies, assembly))
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

    static bool TryGetAssemblies(Mod mod, out IList assemblies) {
        assemblies = null!;
        var value = typeof(Mod).GetField("assemblies",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(mod);
        if (value is not IList list)
            return false;
        assemblies = list;
        return true;
    }

    static bool Contains(IEnumerable assemblies, Assembly assembly) =>
        assemblies.Cast<object?>().Any(item => ReferenceEquals(item, assembly));
}
