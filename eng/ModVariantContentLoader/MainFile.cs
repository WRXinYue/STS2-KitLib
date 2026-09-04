using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using KitLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.ModVariantLoader.ContentEntry;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    const string LoaderSuffix = ".Loader";
    const string KitLibFolderName = "KitLib";

    public static void Initialize() {
        var hostAssembly = typeof(MainFile).Assembly;
        var modDir = Path.GetDirectoryName(hostAssembly.Location)
            ?? throw new InvalidOperationException("Content loader assembly has no Location.");

        var modId = ResolveModId(hostAssembly, modDir);
        var logPrefix = $"{modId}.Loader";

        LinuxHarmonyNativePreloader.EnsureLoaded(
            message => Log.Info($"[{logPrefix}] {message}"),
            message => Log.Warn($"[{logPrefix}] {message}"));

        var implFile = modId + ".dll";
        var variantDir = VariantDirectoryPicker.TryPick(modDir, HostVersionProbe.Numeric, implFile);
        if (variantDir is null) {
            var libRoot = Path.Combine(modDir, VariantDirectoryPicker.LibDirectoryName);
            throw new FileNotFoundException(
                $"No compatible {implFile} under {libRoot} " +
                $"(host={HostVersionProbe.ReleaseLabel ?? HostVersionProbe.Numeric?.ToString() ?? "unknown"}).");
        }

        var implPath = Path.Combine(variantDir, implFile);
        if (!File.Exists(implPath))
            throw new FileNotFoundException($"Missing implementation DLL: {implPath}");

        var context = AssemblyLoadContext.GetLoadContext(hostAssembly) ?? AssemblyLoadContext.Default;
        var kitLibDir = Path.GetFullPath(Path.Combine(modDir, "..", KitLibFolderName));
        var kitLibVariant = Directory.Exists(kitLibDir)
            ? VariantDirectoryPicker.TryPick(kitLibDir, HostVersionProbe.Numeric)
            : null;

        ContentAssemblyResolve.Hook(context, kitLibVariant, variantDir, Directory.Exists(kitLibDir) ? kitLibDir : null);
        LoadImplementation(context, modId, implPath, logPrefix);
    }

    static string ResolveModId(Assembly hostAssembly, string modDir) {
        var name = hostAssembly.GetName().Name ?? "";
        if (name.EndsWith(LoaderSuffix, StringComparison.OrdinalIgnoreCase))
            return name[..^LoaderSuffix.Length];

        var folder = Path.GetFileName(modDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(folder) ? name : folder;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void LoadImplementation(AssemblyLoadContext context, string modId, string implPath, string logPrefix) {
        var fullPath = Path.GetFullPath(implPath);
        Log.Info($"[{logPrefix}] Host version label={HostVersionProbe.ReleaseLabel ?? "<none>"} " +
                 $"numeric={HostVersionProbe.Numeric?.ToString() ?? "<none>"}; loading {fullPath}.");

        var realAsm = context.LoadFromAssemblyPath(fullPath);
        VariantAssemblyAssociation.Associate(modId, realAsm);
        InvokeRealInitializer(realAsm, logPrefix);
    }

    static void InvokeRealInitializer(Assembly realAsm, string logPrefix) {
        Type[] types;
        try {
            types = realAsm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex) {
            Log.Error($"[{logPrefix}] ReflectionTypeLoadException while scanning {realAsm.FullName}: {ex}");
            if (ex.Types is not null) {
                foreach (var t in ex.Types.Where(static x => x is not null))
                    TryInvokeInitializerOnType(t!, logPrefix);
            }
            return;
        }

        if (types.Any(t => TryInvokeInitializerOnType(t, logPrefix)))
            return;

        throw new InvalidOperationException(
            $"No type with {nameof(ModInitializerAttribute)} found in {realAsm.FullName}.");
    }

    static bool TryInvokeInitializerOnType(Type t, string logPrefix) {
        var attr = t.GetCustomAttribute<ModInitializerAttribute>();
        if (attr is null)
            return false;

        var method = t.GetMethod(attr.initializerMethod,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method is null) {
            Log.Error(
                $"[{logPrefix}] Type {t.FullName} has {nameof(ModInitializerAttribute)} but no static method {attr.initializerMethod}.");
            return false;
        }

        method.Invoke(null, null);
        return true;
    }
}
