using KitLib.Abstractions.Modding;

namespace KitLib.Host;

/// <summary>
/// STS2 loads only the main mod DLL; sibling dependencies in the active variant folder
/// are resolved from that directory (Semver + Abstractions facade).
/// </summary>
internal static class ModDependencyLoader {
    internal const string AbstractionsFileName = "KitLib.Abstractions.dll";

    internal static readonly string[] VariantRuntimeDeps = [
        "Microsoft.Extensions.Primitives.dll",
        "Semver.dll",
        AbstractionsFileName,
    ];

    internal static bool TryBootstrapFromDirectory(string variantDir, bool log) {
        if (string.IsNullOrEmpty(variantDir))
            return false;

        var modFolder = KitLibHostPaths.ResolveModFolder(variantDir);
        ModAssemblyLoader.EnsureResolveHook(modFolder, [variantDir]);

        foreach (var fileName in VariantRuntimeDeps) {
            var path = Path.Combine(variantDir, fileName);
            if (!File.Exists(path))
                continue;
            ModAssemblyLoader.LoadFromModPath(path);
            if (log && fileName == AbstractionsFileName)
                MainFile.Logger.Info("Loaded KitLib.Abstractions facade");
        }

        return true;
    }

    internal static void EnsureLoaded() {
        var variantDir = Path.GetDirectoryName(typeof(MainFile).Assembly.Location);
        if (string.IsNullOrEmpty(variantDir)) {
            MainFile.Logger.Warn("Cannot resolve Core directory for dependency loading.");
            return;
        }

        KitLibHostPaths.SetActiveVariantRoot(variantDir);
        TryBootstrapFromDirectory(variantDir, log: true);
    }
}
