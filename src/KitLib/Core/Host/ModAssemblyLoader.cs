using System.Reflection;
using System.Runtime.Loader;
using KitLib.Abstractions.Modding;

namespace KitLib.Host;

/// <summary>
/// Loads KitLib sibling/satellite DLLs into the same <see cref="AssemblyLoadContext"/> as Core.
/// STS2 hosts the main mod DLL in a dedicated context; LoadFrom would bind satellites to a
/// different KitLib copy and cause MissingMethodException at runtime.
/// </summary>
internal static class ModAssemblyLoader {
    static AssemblyLoadContext? _modContext;
    static string? _modDir;
    static IReadOnlyList<string> _searchDirs = [];
    static bool _resolveHooked;
    static readonly HashSet<AssemblyLoadContext> _contextHooks = [];

    internal static AssemblyLoadContext ModContext =>
        _modContext ??= AssemblyLoadContext.GetLoadContext(typeof(ModAssemblyLoader).Assembly)
                        ?? AssemblyLoadContext.Default;

    internal static void EnsureResolveHook(string? modDir = null, IReadOnlyList<string>? searchDirs = null) {
        if (!string.IsNullOrEmpty(modDir))
            _modDir = modDir;
        if (searchDirs is { Count: > 0 })
            _searchDirs = searchDirs;
        else if (!string.IsNullOrEmpty(_modDir) && _searchDirs.Count == 0)
            _searchDirs = KitLibHostPaths.EnumerateModuleSearchDirectories(_modDir);

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
        var simple = name.Name;
        if (string.IsNullOrEmpty(simple))
            return null;

        var existing = FindLoaded(simple);
        if (existing != null)
            return existing;

        var path = FindDependencyPath(name, context);
        if (path == null)
            return null;

        return LoadMatching(context, path, simple);
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
                var loaded = LoadMatching(context, path, simple);
                if (loaded != null)
                    return loaded;
            }
            catch (FileNotFoundException) {
            }
            catch (FileLoadException) {
                return FindLoaded(simple);
            }
        }

        return null;
    }

    static string? FindDependencyPath(AssemblyName name, AssemblyLoadContext? context = null) {
        var modDir = ResolveModDir(context);
        var simple = name.Name;
        if (string.IsNullOrEmpty(simple))
            return null;

        if (!string.IsNullOrEmpty(modDir)) {
            if (string.Equals(simple, "KitLib.Core", StringComparison.OrdinalIgnoreCase))
                return KitLibHostPaths.TryResolveCoreAssemblyPath(modDir);

            if (string.Equals(simple, "KitLib", StringComparison.OrdinalIgnoreCase)) {
                var loader = Path.Combine(KitLibHostPaths.ResolveModFolder(modDir), "KitLib.dll");
                if (File.Exists(loader))
                    return Path.GetFullPath(loader);
            }
        }

        foreach (var dir in ResolveSearchDirectories(modDir)) {
            var path = Path.Combine(dir, simple + ".dll");
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        return null;
    }

    static IEnumerable<string> ResolveSearchDirectories(string? modDir) {
        if (_searchDirs.Count > 0)
            return _searchDirs;

        if (!string.IsNullOrEmpty(modDir))
            return KitLibHostPaths.EnumerateModuleSearchDirectories(modDir);

        return [];
    }

    static string? ResolveModDir(AssemblyLoadContext? context) {
        if (!string.IsNullOrEmpty(_modDir))
            return _modDir;

        if (context == null)
            return null;

        foreach (var asm in context.Assemblies) {
            if (!string.Equals(asm.GetName().Name, "KitLib", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(asm.GetName().Name, "KitLib.Core", StringComparison.OrdinalIgnoreCase))
                continue;

            var dir = Path.GetDirectoryName(asm.Location);
            if (string.IsNullOrEmpty(dir))
                continue;

            var folder = KitLibHostPaths.ResolveModFolder(dir);
            if (string.Equals(Path.GetFileName(folder), "KitLib", StringComparison.OrdinalIgnoreCase)
                || File.Exists(Path.Combine(folder, "KitLib.dll"))) {
                _modDir = folder;
                return folder;
            }

            var sibling = KitLibHostPaths.ResolveSiblingKitLibModDirectory(folder);
            if (sibling is not null) {
                _modDir = sibling;
                return sibling;
            }

            _modDir = folder;
            return folder;
        }

        return null;
    }

    static Assembly? LoadMatching(AssemblyLoadContext context, string path, string requestedSimple) {
        var fileSimple = AssemblyName.GetAssemblyName(path).Name;
        if (string.IsNullOrEmpty(fileSimple)
            || !string.Equals(fileSimple, requestedSimple, StringComparison.OrdinalIgnoreCase))
            return null;

        return PreloadIntoContext(context, path);
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
