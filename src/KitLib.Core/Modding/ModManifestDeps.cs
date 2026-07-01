using System.Linq;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Modding;

internal static class ModManifestDeps {
    internal static string[] Copy(ModManifest manifest) {
        if (manifest.dependencies is not { Count: > 0 } deps)
            return [];

        return deps
            .Where(d => d != null && !string.IsNullOrEmpty(d.id))
            .Select(d => string.IsNullOrEmpty(d.minVersion) ? d.id : $"{d.id}>={d.minVersion}")
            .ToArray();
    }
}
