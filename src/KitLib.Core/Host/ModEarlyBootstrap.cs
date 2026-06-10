using System.Runtime.CompilerServices;

namespace KitLib.Host;

/// <summary>
/// STS2 reflects over KitLib.dll before <see cref="MainFile.Initialize"/> runs; preload sibling DLLs
/// into the mod <see cref="System.Runtime.Loader.AssemblyLoadContext"/> as soon as Core loads.
/// </summary>
internal static class ModEarlyBootstrap {
    [ModuleInitializer]
    internal static void OnKitLibAssemblyLoad() {
        var modDir = Path.GetDirectoryName(typeof(ModEarlyBootstrap).Assembly.Location);
        if (string.IsNullOrEmpty(modDir))
            return;

        ModDependencyLoader.TryBootstrapFromDirectory(modDir, log: false);
    }
}
