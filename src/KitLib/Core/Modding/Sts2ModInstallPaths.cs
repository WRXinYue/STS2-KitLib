using System;
using System.IO;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Modding;

/// <summary>
/// Resolves product folders from official <see cref="ModManager"/> entries
/// (<c>Mod.manifest.id</c> + <c>Mod.path</c>), including Steam Workshop numeric dirs.
/// </summary>
internal static class Sts2ModInstallPaths {
    internal static void Register() {
        KitLibHostPaths.GameModDirectoryResolver = TryResolve;
    }

    internal static string? TryResolve(string productId) {
        if (string.IsNullOrWhiteSpace(productId))
            return null;

        try {
            string? pending = null;
            foreach (var mod in ModManager.Mods) {
                var id = mod.manifest?.id;
                if (id is null || !string.Equals(id, productId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (mod.state is ModLoadState.Disabled or ModLoadState.DisabledDuplicate or ModLoadState.AddedAtRuntime)
                    continue;
                if (string.IsNullOrWhiteSpace(mod.path))
                    continue;

                var folder = KitLibHostPaths.ResolveModFolder(mod.path);
                if (!Directory.Exists(folder))
                    continue;

                if (mod.state == ModLoadState.Loaded)
                    return folder;
                pending ??= folder;
            }

            return pending;
        }
        catch (Exception) {
            return null;
        }
    }
}
