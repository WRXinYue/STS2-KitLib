using KitLib;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Modding;

internal static class Sts2ModCatalogDeps {
    internal static string[] CopyDependencies(ModManifest manifest) =>
        Sts2ManifestCompat.CopyDependencies(manifest);
}
