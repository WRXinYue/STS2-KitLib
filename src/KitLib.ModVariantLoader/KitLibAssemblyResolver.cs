using System.Reflection;
using System.Runtime.Loader;
using KitLib.Abstractions.Modding;

namespace KitLib.ModVariantLoader;

/// <summary>
/// Resolves KitLib shared DLLs for content mods whose ALC is created after KitLib core boots.
/// </summary>
internal static class KitLibAssemblyResolver {
    static readonly HashSet<AssemblyLoadContext> HookedContexts = [];
    static string? _kitLibModDir;
    static bool _domainHooked;

    internal static void EnsureHooked(Assembly hostAssembly) {
        var kitLibDir = ResolveKitLibModDirectory(hostAssembly);
        if (!string.IsNullOrEmpty(kitLibDir))
            _kitLibModDir = kitLibDir;

        var alc = AssemblyLoadContext.GetLoadContext(hostAssembly) ?? AssemblyLoadContext.Default;
        HookContext(alc);

        var bootstrapAlc = AssemblyLoadContext.GetLoadContext(typeof(ModVariantBootstrap).Assembly);
        if (bootstrapAlc != null)
            HookContext(bootstrapAlc);

        if (_domainHooked)
            return;

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            Resolve(AssemblyLoadContext.Default, new AssemblyName(args.Name));
        _domainHooked = true;
    }

    static void HookContext(AssemblyLoadContext context) {
        if (!HookedContexts.Add(context))
            return;

        context.Resolving += (ctx, name) => Resolve(ctx, name);
    }

    static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name) {
        foreach (var alc in AssemblyLoadContext.All)
            HookContext(alc);

        var simple = name.Name;
        if (string.IsNullOrEmpty(simple) || string.IsNullOrEmpty(_kitLibModDir))
            return null;

        var existing = FindLoadedInContext(context, simple);
        if (existing != null)
            return existing;

        var path = FindDependencyPath(simple);
        if (path is null)
            return null;

        try {
            return context.LoadFromAssemblyPath(path);
        }
        catch (FileLoadException) {
            return null;
        }
        catch (FileNotFoundException) {
            return null;
        }
    }

    static string? FindDependencyPath(string simpleName) {
        if (string.Equals(simpleName, "KitLib", StringComparison.OrdinalIgnoreCase)) {
            var flat = Path.Combine(_kitLibModDir!, simpleName + ".dll");
            if (File.Exists(flat))
                return Path.GetFullPath(flat);
        }

        foreach (var dir in new[] {
                     Path.Combine(_kitLibModDir!, "modules"),
                     _kitLibModDir!,
                 }) {
            var path = Path.Combine(dir, simpleName + ".dll");
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        return null;
    }

    static string? ResolveKitLibModDirectory(Assembly hostAssembly) {
        var hostDir = Path.GetDirectoryName(hostAssembly.Location);
        if (string.IsNullOrEmpty(hostDir))
            return null;

        if (string.Equals(hostAssembly.GetName().Name, "KitLib.ModVariantLoader", StringComparison.OrdinalIgnoreCase))
            return ModVariantAssemblyPaths.ResolveSiblingKitLibModDirectory(hostDir) ?? hostDir;

        return ModVariantAssemblyPaths.ResolveSiblingKitLibModDirectory(hostDir);
    }

    static Assembly? FindLoadedInContext(AssemblyLoadContext context, string simpleName) {
        foreach (var asm in context.Assemblies) {
            if (string.Equals(asm.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                return asm;
        }

        return null;
    }
}
