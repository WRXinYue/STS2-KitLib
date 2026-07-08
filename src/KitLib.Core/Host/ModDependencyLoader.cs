namespace KitLib.Host;

/// <summary>
/// STS2 loads only the main mod DLL; sibling dependencies must be loaded explicitly.
/// </summary>
internal static class ModDependencyLoader {
    internal const string AbstractionsFileName = "KitLib.Abstractions.dll";

    /// <summary>Runtime deps for <see cref="KitLib.Abstractions"/> (semver ranges, profile map).</summary>
    internal static readonly string[] AbstractionsRuntimeDeps = [
        "Microsoft.Extensions.Primitives.dll",
        "Semver.dll",
        AbstractionsFileName,
    ];

    /// <summary>Shared runtime for content mods using dual API variant loaders.</summary>
    internal const string ModVariantLoaderFileName = "KitLib.ModVariantLoader.dll";

    /// <summary>Registers resolve hooks and preloads Abstractions when present.</summary>
    internal static bool TryBootstrapFromDirectory(string modDir, bool log) {
        if (string.IsNullOrEmpty(modDir))
            return false;

        ModAssemblyLoader.EnsureResolveHook(modDir);

        var abstractions = Path.Combine(modDir, AbstractionsFileName);
        if (!File.Exists(abstractions)) {
            if (log)
                MainFile.Logger.Warn($"Missing dependency DLL: {abstractions}");
            return false;
        }

        foreach (var fileName in AbstractionsRuntimeDeps) {
            var path = Path.Combine(modDir, fileName);
            if (!File.Exists(path)) {
                if (log && fileName == AbstractionsFileName)
                    MainFile.Logger.Warn($"Missing dependency DLL: {path}");
                continue;
            }
            ModAssemblyLoader.LoadFromModPath(path);
            if (log && fileName == AbstractionsFileName)
                MainFile.Logger.Info("Loaded mod dependency: KitLib.Abstractions");
        }

        var variantLoader = Path.Combine(modDir, ModVariantLoaderFileName);
        if (File.Exists(variantLoader)) {
            ModAssemblyLoader.LoadFromModPath(variantLoader);
            if (log)
                MainFile.Logger.Info("Loaded mod dependency: KitLib.ModVariantLoader");
        }

        return true;
    }

    internal static void EnsureLoaded() {
        var modDir = ModPaths.ResolveModRoot(typeof(MainFile).Assembly);
        if (string.IsNullOrEmpty(modDir)) {
            MainFile.Logger.Warn("Cannot resolve mod directory for dependency loading.");
            return;
        }

        TryBootstrapFromDirectory(modDir, log: true);
    }
}
