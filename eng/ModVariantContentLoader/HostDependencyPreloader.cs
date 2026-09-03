using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace KitLib.ModVariantLoader.ContentEntry;

/// <summary>
/// Loads KitLib host DLLs before the content loader JITs types from ModVariantLoader.
/// STS2 mod ALCs do not probe sibling mod folders automatically.
/// </summary>
internal static class HostDependencyPreloader {
    static readonly string[] HostDeps = [
        "Microsoft.Extensions.Primitives.dll",
        "Semver.dll",
        "KitLib.Abstractions.dll",
        "KitLib.ModVariantLoader.dll",
    ];

    static bool _hooked;

    [ModuleInitializer]
    internal static void InitializeModule() => Ensure(Assembly.GetExecutingAssembly());

    internal static void Ensure(Assembly hostAssembly) {
        if (_hooked)
            return;

        var hostDir = Path.GetDirectoryName(hostAssembly.Location);
        if (string.IsNullOrEmpty(hostDir))
            return;

        var searchDirs = CollectSearchDirectories(hostDir);
        HookAll(searchDirs);
        Preload(hostAssembly, searchDirs);
        _hooked = true;
    }

    static List<string> CollectSearchDirectories(string hostDir) {
        var dirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? dir) {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return;
            var full = Path.GetFullPath(dir);
            if (seen.Add(full))
                dirs.Add(full);
        }

        Add(hostDir);
        Add(Path.GetFullPath(Path.Combine(hostDir, "..", "KitLib")));
        return dirs;
    }

    static void HookAll(IReadOnlyList<string> searchDirs) {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            LoadForRequest(args.Name, args.RequestingAssembly, searchDirs);

        foreach (var context in AssemblyLoadContext.All)
            context.Resolving += (_, name) => LoadIntoContext(context, name.Name, searchDirs);
    }

    static Assembly? LoadForRequest(string? requestedName, Assembly? requesting, IReadOnlyList<string> searchDirs) {
        var path = Resolve(searchDirs, requestedName);
        if (path is null)
            return null;

        var context = requesting is null ? null : AssemblyLoadContext.GetLoadContext(requesting);
        context ??= AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
        return context?.LoadFromAssemblyPath(path);
    }

    static Assembly? LoadIntoContext(AssemblyLoadContext context, string? simpleName, IReadOnlyList<string> searchDirs) {
        var path = Resolve(searchDirs, simpleName);
        return path is null ? null : context.LoadFromAssemblyPath(path);
    }

    static void Preload(Assembly hostAssembly, IReadOnlyList<string> searchDirs) {
        var context = AssemblyLoadContext.GetLoadContext(hostAssembly);
        if (context is null)
            return;

        foreach (var fileName in HostDeps) {
            var simple = Path.GetFileNameWithoutExtension(fileName);
            if (FindLoaded(context, simple) is not null)
                continue;

            var path = Resolve(searchDirs, simple);
            if (path is null)
                continue;

            context.LoadFromAssemblyPath(path);
        }
    }

    static string? Resolve(IReadOnlyList<string> searchDirs, string? requestedName) {
        if (string.IsNullOrEmpty(requestedName))
            return null;

        var simpleName = new AssemblyName(requestedName).Name;
        if (string.IsNullOrEmpty(simpleName))
            return null;

        foreach (var dir in searchDirs) {
            var path = Path.Combine(dir, simpleName + ".dll");
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        return null;
    }

    static Assembly? FindLoaded(AssemblyLoadContext context, string simpleName) {
        foreach (var asm in context.Assemblies) {
            if (string.Equals(asm.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                return asm;
        }

        return null;
    }
}
