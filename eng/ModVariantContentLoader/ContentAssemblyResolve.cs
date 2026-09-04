using System.Reflection;
using System.Runtime.Loader;

namespace KitLib.ModVariantLoader.ContentEntry;

/// <summary>
/// Prefer assemblies already loaded (KitLib's ALC), then load from picked variant folders.
/// </summary>
internal static class ContentAssemblyResolve {
    internal static void Hook(AssemblyLoadContext context, params string?[] searchDirs) {
        var dirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in searchDirs) {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                continue;
            var full = Path.GetFullPath(dir);
            if (seen.Add(full))
                dirs.Add(full);
        }

        context.Resolving += (_, name) => Resolve(context, name.Name, dirs);
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) => {
            var alc = args.RequestingAssembly is null
                ? context
                : AssemblyLoadContext.GetLoadContext(args.RequestingAssembly) ?? context;
            return Resolve(alc, args.Name, dirs);
        };
    }

    static Assembly? Resolve(AssemblyLoadContext context, string? requestedName, List<string> dirs) {
        if (string.IsNullOrEmpty(requestedName))
            return null;

        var simple = requestedName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(requestedName)
            : new AssemblyName(requestedName).Name;
        if (string.IsNullOrEmpty(simple))
            return null;

        foreach (var alc in AssemblyLoadContext.All) {
            var loaded = FindLoaded(alc, simple);
            if (loaded is not null)
                return loaded;
        }

        foreach (var dir in dirs) {
            var path = Path.Combine(dir, simple + ".dll");
            if (!File.Exists(path))
                continue;
            try {
                return context.LoadFromAssemblyPath(Path.GetFullPath(path));
            }
            catch (FileLoadException) {
                return FindLoaded(context, simple);
            }
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
