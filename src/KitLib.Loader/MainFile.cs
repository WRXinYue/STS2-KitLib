using System.Reflection;
using System.Runtime.Loader;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitLib";

    static readonly string[] HostDeps = [
        "Microsoft.Extensions.Primitives.dll",
        "Semver.dll",
        "KitLib.Abstractions.dll",
        "KitLib.ModVariantLoader.dll",
    ];

    public static void Initialize() {
        var hostAssembly = typeof(MainFile).Assembly;
        var modDir = Path.GetDirectoryName(hostAssembly.Location);
        if (string.IsNullOrEmpty(modDir))
            throw new InvalidOperationException("KitLib loader assembly has no Location.");

        var alc = AssemblyLoadContext.GetLoadContext(hostAssembly) ?? AssemblyLoadContext.Default;
        PreloadDependencies(alc, modDir);

        var loaderAsm = FindLoaded(alc, "KitLib.ModVariantLoader")
                        ?? throw new FileNotFoundException("KitLib.ModVariantLoader not loaded.");

        var optionsType = loaderAsm.GetType("KitLib.ModVariantLoader.ModVariantBootstrapOptions", throwOnError: true)!;
        var options = Activator.CreateInstance(optionsType)!;
        optionsType.GetProperty("ModId")!.SetValue(options, ModId);
        optionsType.GetProperty("ImplementationAssemblyFileName")!.SetValue(options, ModVariantLayout.KitLibHostCoreFileName);
        optionsType.GetProperty("LogPrefix")!.SetValue(options, "KitLib.Loader");
        optionsType.GetProperty("HarmonyId")!.SetValue(options, "KitLib.ModVariantLoader.KitLib");

        var bootstrap = loaderAsm.GetType("KitLib.ModVariantLoader.ModVariantBootstrap", throwOnError: true)!;
        bootstrap.GetMethod("Initialize", [optionsType])!.Invoke(null, [options]);
    }

    static void PreloadDependencies(AssemblyLoadContext alc, string modDir) {
        foreach (var fileName in HostDeps) {
            var path = Path.Combine(modDir, fileName);
            if (!File.Exists(path))
                continue;

            var simple = Path.GetFileNameWithoutExtension(fileName);
            if (FindLoaded(alc, simple) is not null)
                continue;

            alc.LoadFromAssemblyPath(Path.GetFullPath(path));
        }
    }

    static Assembly? FindLoaded(AssemblyLoadContext alc, string simpleName) {
        foreach (var asm in alc.Assemblies) {
            if (string.Equals(asm.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                return asm;
        }

        return null;
    }
}
