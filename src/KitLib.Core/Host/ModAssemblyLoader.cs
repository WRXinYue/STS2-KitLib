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
    static readonly HashSet<AssemblyLoadContext> _contextHooks = [];

    internal static AssemblyLoadContext ModContext =>
        _modContext ??= AssemblyLoadContext.GetLoadContext(typeof(ModAssemblyLoader).Assembly)
                        ?? AssemblyLoadContext.Default;

    internal static void EnsureResolveHook(string? modDir = null) {
        if (!string.IsNullOrEmpty(modDir))
            _modDir = modDir;
        if (_resolveHooked)
            return;

        AssemblyLoadContext.Default.Resolving += OnModContextResolving;
        foreach (var context in AssemblyLoadContext.All)
            HookContext(context);

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

    static void HookContext(AssemblyLoadContext context) {
        if (_contextHooks.Contains(context))
            return;

        context.Resolving += OnModContextResolving;
        _contextHooks.Add(context);
    }

    static Assembly? OnModContextResolving(AssemblyLoadContext context, AssemblyName name) {
        HookContext(context);
        var path = FindDependencyPath(name, context);
        if (path == null)
            return null;

        return PreloadIntoContext(context, path);
    }

    static Assembly? Resolve(string? requestedName) {
        if (string.IsNullOrEmpty(requestedName))
            return null;

        var name = new AssemblyName(requestedName);
        var simple = name.Name;
        if (string.IsNullOrEmpty(simple))
            return null;

        var existing = FindLoaded(simple);
        if (existing != null)
            return existing;

        foreach (var context in AssemblyLoadContext.All) {
            var path = FindDependencyPath(name, context);
            if (path == null)
                continue;
            try {
                return PreloadIntoContext(context, path);
            }
            catch (FileNotFoundException) {
            }
        }

        return null;
    }

    static string? FindDependencyPath(AssemblyName name, AssemblyLoadContext? context = null) {
        var modDir = ResolveModDir(context);
        if (string.IsNullOrEmpty(modDir))
            return null;

        var simple = name.Name;
        if (string.IsNullOrEmpty(simple))
            return null;

        if (string.Equals(simple, "KitLib", StringComparison.OrdinalIgnoreCase)) {
            var flat = Path.Combine(modDir, simple + ".dll");
            if (File.Exists(flat))
                return Path.GetFullPath(flat);
        }

        foreach (var dir in new[] { Path.Combine(modDir, SatelliteModuleLoader.ModulesSubdir), modDir }) {
            var path = Path.Combine(dir, simple + ".dll");
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        return null;
    }

    static string? ResolveModDir(AssemblyLoadContext? context) {
        if (!string.IsNullOrEmpty(_modDir))
            return _modDir;

        if (context == null)
            return null;

        foreach (var asm in context.Assemblies) {
            if (!string.Equals(asm.GetName().Name, "KitLib", StringComparison.OrdinalIgnoreCase))
                continue;

            var dir = Path.GetDirectoryName(asm.Location);
            if (!string.IsNullOrEmpty(dir)) {
                _modDir = dir;
                return dir;
            }
        }

        return null;
    }

    static Assembly PreloadIntoContext(AssemblyLoadContext context, string path) {
        var simple = AssemblyName.GetAssemblyName(path).Name;
        var existing = FindLoadedInContext(context, simple);
        if (existing != null)
            return existing;

        return context.LoadFromAssemblyPath(path);
    }

    internal static Assembly? GetLoadedAssembly(string? simpleName) => FindLoaded(simpleName);

    static Assembly? FindLoaded(string? simpleName) {
        if (string.IsNullOrEmpty(simpleName))
            return null;

        var existing = FindLoadedInContext(ModContext, simpleName);
        if (existing != null)
            return existing;

        foreach (var context in AssemblyLoadContext.All) {
            existing = FindLoadedInContext(context, simpleName);
            if (existing != null)
                return existing;
        }

        return null;
    }

    static Assembly? FindLoadedInContext(AssemblyLoadContext context, string? simpleName) {
        if (string.IsNullOrEmpty(simpleName))
            return null;

        foreach (var asm in context.Assemblies) {
            var name = asm.GetName().Name;
            if (name != null && string.Equals(name, simpleName, StringComparison.OrdinalIgnoreCase))
                return asm;
        }

        return null;
    }
}
