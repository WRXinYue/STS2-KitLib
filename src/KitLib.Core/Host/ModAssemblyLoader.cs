using System.Reflection;
using System.Runtime.Loader;

namespace KitLib.Host;

/// <summary>
/// Loads KitLib sibling/satellite DLLs into the same <see cref="AssemblyLoadContext"/> as Core.
/// STS2 hosts the main mod DLL in a dedicated context; LoadFrom would bind satellites to a
/// different KitLib copy and cause MissingMethodException at runtime.
/// </summary>
internal static class ModAssemblyLoader {
    static AssemblyLoadContext? _modContext;
    static string? _modDir;
    static bool _resolveHooked;

    internal static AssemblyLoadContext ModContext =>
        _modContext ??= AssemblyLoadContext.GetLoadContext(typeof(ModAssemblyLoader).Assembly)
                        ?? AssemblyLoadContext.Default;

    internal static void EnsureResolveHook(string modDir) {
        _modDir = modDir;
        if (_resolveHooked)
            return;

        ModContext.Resolving += OnModContextResolving;
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) => Resolve(args.Name);
        _resolveHooked = true;
    }

    internal static Assembly LoadFromModPath(string path) {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Mod assembly not found: {path}");

        var simple = AssemblyName.GetAssemblyName(path).Name;
        var existing = FindLoaded(simple);
        if (existing != null)
            return existing;

        return ModContext.LoadFromAssemblyPath(Path.GetFullPath(path));
    }

    static Assembly? OnModContextResolving(AssemblyLoadContext context, AssemblyName name) =>
        Resolve(name.FullName);

    static Assembly? Resolve(string? requestedName) {
        if (string.IsNullOrEmpty(requestedName) || string.IsNullOrEmpty(_modDir))
            return null;

        var simple = new AssemblyName(requestedName).Name;
        if (string.IsNullOrEmpty(simple))
            return null;

        var existing = FindLoaded(simple);
        if (existing != null)
            return existing;

        foreach (var dir in new[] { _modDir, Path.Combine(_modDir, SatelliteModuleLoader.ModulesSubdir) }) {
            var path = Path.Combine(dir, simple + ".dll");
            if (!File.Exists(path))
                continue;
            return ModContext.LoadFromAssemblyPath(Path.GetFullPath(path));
        }

        return null;
    }

    static Assembly? FindLoaded(string? simpleName) {
        if (string.IsNullOrEmpty(simpleName))
            return null;

        var ctx = ModContext;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            if (AssemblyLoadContext.GetLoadContext(asm) != ctx)
                continue;
            var name = asm.GetName().Name;
            if (name != null && string.Equals(name, simpleName, StringComparison.OrdinalIgnoreCase))
                return asm;
        }

        return null;
    }
}
