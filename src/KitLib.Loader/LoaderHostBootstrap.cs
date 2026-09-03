using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace KitLib;

/// <summary>
/// Runs when KitLib.dll is loaded, before any mod initializer is JITted.
/// STS2 hosts each mod in its own ALC; probing only the mod folder is not enough for JIT.
/// </summary>
internal static class LoaderHostBootstrap {
    static readonly string[] HostDeps = [
        "Microsoft.Extensions.Primitives.dll",
        "Semver.dll",
        "KitLib.Abstractions.dll",
        "KitLib.ModVariantLoader.dll",
    ];

    static bool _hooked;

    [ModuleInitializer]
    internal static void InitializeModule() {
        var host = Assembly.GetExecutingAssembly();
        var modDir = Path.GetDirectoryName(host.Location);
        if (string.IsNullOrEmpty(modDir))
            return;

        Ensure(modDir);
    }

    internal static void Ensure(string modDir) {
        if (_hooked)
            return;

        var searchDirs = new[] { Path.GetFullPath(modDir) };
        HookAll(searchDirs);
        Preload(Assembly.GetExecutingAssembly(), searchDirs);
        _hooked = true;
    }

    static void HookAll(IReadOnlyList<string> searchDirs) {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            LoadForRequest(args.Name, args.RequestingAssembly, searchDirs);

        foreach (var context in AssemblyLoadContext.All)
            HookContext(context, searchDirs);
    }

    static void HookContext(AssemblyLoadContext context, IReadOnlyList<string> searchDirs) {
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

    static void Preload(Assembly host, IReadOnlyList<string> searchDirs) {
        var context = AssemblyLoadContext.GetLoadContext(host);
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

    static string? Resolve(IReadOnlyList<string> searchDirs, string? simpleName) {
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
