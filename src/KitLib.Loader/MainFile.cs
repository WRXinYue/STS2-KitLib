using System.Reflection;
using System.Runtime.Loader;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitLib";
    public const string CoreFileName = "KitLib.Core.dll";

    public static void Initialize() {
        var hostAssembly = typeof(MainFile).Assembly;
        var modDir = Path.GetDirectoryName(hostAssembly.Location)
            ?? throw new InvalidOperationException("KitLib loader assembly has no Location.");

        LoaderHostBootstrap.Ensure(modDir);

        var alc = AssemblyLoadContext.GetLoadContext(hostAssembly)
            ?? throw new InvalidOperationException("KitLib loader assembly has no AssemblyLoadContext.");

        var corePath = ResolveCoreAssemblyPath(modDir);
        var coreAsm = alc.LoadFromAssemblyPath(corePath);
        ModAssemblyAssociation.Associate(ModId, coreAsm);
        InvokeCoreInitializer(coreAsm);
    }

    static string ResolveCoreAssemblyPath(string modDir) {
        var variantRoot = KitLibHostPaths.TryPickVariantDirectory(modDir, Sts2HostVersion.Numeric);
        if (variantRoot is null) {
            throw new FileNotFoundException(
                $"KitLib core assembly not found under {Path.Combine(modDir, KitLibHostPaths.LibDirectoryName)}/<api>/{CoreFileName}.");
        }

        KitLibHostPaths.SetActiveVariantRoot(variantRoot);
        var corePath = Path.Combine(variantRoot, CoreFileName);
        if (!File.Exists(corePath))
            throw new FileNotFoundException($"KitLib core assembly not found: {corePath}");

        return Path.GetFullPath(corePath);
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
}
