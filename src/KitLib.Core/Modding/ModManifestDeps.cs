using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Modding;

internal static class ModManifestDeps {
    internal static string[] Copy(ModManifest manifest) =>
        Sts2ManifestCompat.CopyDependencies(manifest);
}
