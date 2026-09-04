using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitLib";
    public const string CoreFileName = VariantDirectoryPicker.CoreFileName;

    public static void Initialize() {
        var hostAssembly = typeof(MainFile).Assembly;
        var modDir = Path.GetDirectoryName(hostAssembly.Location)
            ?? throw new InvalidOperationException("KitLib loader assembly has no Location.");

        LinuxHarmonyNativePreloader.EnsureLoaded(
            message => Log.Info($"[KitLib.Loader] {message}"),
            message => Log.Warn($"[KitLib.Loader] {message}"));

        var variantDir = VariantDirectoryPicker.TryPick(modDir, HostVersionProbe.Numeric);
        if (variantDir is null) {
            var libRoot = Path.Combine(modDir, VariantDirectoryPicker.LibDirectoryName);
            throw new FileNotFoundException(
                "KitLib variant implementation was not found. Reinstall the full KitLib folder " +
                $"(KitLib.dll plus lib/<api>/{CoreFileName}). Looked under {libRoot} " +
                $"(host={HostVersionProbe.ReleaseLabel ?? HostVersionProbe.Numeric?.ToString() ?? "unknown"}).");
        }

        var corePath = Path.Combine(variantDir, CoreFileName);
        if (!File.Exists(corePath)) {
            throw new FileNotFoundException(
                $"KitLib variant directory is incomplete: missing {corePath}. Reinstall that lib/<api>/ folder.");
        }

        var context = AssemblyLoadContext.GetLoadContext(hostAssembly) ?? AssemblyLoadContext.Default;
        VariantDirectoryAlc.Hook(context, variantDir);
        LoadCore(context, corePath);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void LoadCore(AssemblyLoadContext context, string corePath) {
        var fullPath = Path.GetFullPath(corePath);
        Log.Info($"[KitLib.Loader] Host version label={HostVersionProbe.ReleaseLabel ?? "<none>"} " +
                 $"numeric={HostVersionProbe.Numeric?.ToString() ?? "<none>"}; loading {fullPath}.");

        var core = context.LoadFromAssemblyPath(fullPath);
        VariantAssemblyAssociation.Associate(ModId, core);
        InvokeRealInitializer(core);
    }

    static void InvokeRealInitializer(Assembly realAsm) {
        Type[] types;
        try {
            types = realAsm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex) {
            Log.Error($"[KitLib.Loader] ReflectionTypeLoadException while scanning {realAsm.FullName}: {ex}");
            if (ex.Types is not null) {
                foreach (var t in ex.Types.Where(static x => x is not null))
                    TryInvokeInitializerOnType(t!);
            }
            return;
        }

        if (types.Any(TryInvokeInitializerOnType))
            return;

        throw new InvalidOperationException(
            $"No type with {nameof(ModInitializerAttribute)} found in {realAsm.FullName}.");
    }

    static bool TryInvokeInitializerOnType(Type t) {
        var attr = t.GetCustomAttribute<ModInitializerAttribute>();
        if (attr is null)
            return false;

        var method = t.GetMethod(attr.initializerMethod,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method is null) {
            Log.Error(
                $"[KitLib.Loader] Type {t.FullName} has {nameof(ModInitializerAttribute)} but no static method {attr.initializerMethod}.");
            return false;
        }

        method.Invoke(null, null);
        return true;
    }
}
