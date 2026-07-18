using System.Reflection;
using System.Runtime.Loader;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitLib";
    public const string CoreFileName = "KitLib.Core.dll";

    static readonly string[] HostDeps = [
        "Microsoft.Extensions.Primitives.dll",
        "Semver.dll",
        "KitLib.Abstractions.dll",
    ];

    public static void Initialize() {
        var hostAssembly = typeof(MainFile).Assembly;
        var modDir = Path.GetDirectoryName(hostAssembly.Location)
            ?? throw new InvalidOperationException("KitLib loader assembly has no Location.");

        var alc = AssemblyLoadContext.GetLoadContext(hostAssembly) ?? AssemblyLoadContext.Default;
        PreloadDependencies(alc, modDir);

        var corePath = Path.Combine(modDir, CoreFileName);
        if (!File.Exists(corePath))
            throw new FileNotFoundException($"KitLib core assembly not found: {corePath}");

        var coreAsm = alc.LoadFromAssemblyPath(Path.GetFullPath(corePath));
        ModManager.AssociateAssemblyWithMod(ModId, coreAsm);
        InvokeCoreInitializer(coreAsm);
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

    static void InvokeCoreInitializer(Assembly coreAsm) {
        foreach (var type in coreAsm.GetTypes()) {
            var attr = type.GetCustomAttribute<ModInitializerAttribute>();
            if (attr is null)
                continue;

            var method = type.GetMethod(
                attr.initializerMethod,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method is null)
                throw new InvalidOperationException(
                    $"Type {type.FullName} has {nameof(ModInitializerAttribute)} but no static method {attr.initializerMethod}.");

            method.Invoke(null, null);
            return;
        }

        throw new InvalidOperationException(
            $"No type with {nameof(ModInitializerAttribute)} found in {coreAsm.FullName}.");
    }

    static Assembly? FindLoaded(AssemblyLoadContext alc, string simpleName) {
        foreach (var asm in alc.Assemblies) {
            if (string.Equals(asm.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                return asm;
        }

        return null;
    }
}
