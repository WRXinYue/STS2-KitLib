using System.Reflection;
using System.Runtime.Loader;

namespace KitLib;

/// <summary>Resolve sibling DLLs from one picked <c>lib/&lt;api&gt;</c> folder (no filename whitelist).</summary>
internal static class VariantDirectoryAlc {
    internal static void Hook(AssemblyLoadContext context, string variantDir) {
        var dir = Path.GetFullPath(variantDir);
        context.Resolving += (_, name) => LoadFromVariant(context, dir, name.Name);
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) => {
            var requesting = args.RequestingAssembly;
            var alc = requesting is null ? context : AssemblyLoadContext.GetLoadContext(requesting) ?? context;
            return LoadFromVariant(alc, dir, args.Name);
        };
    }

    static Assembly? LoadFromVariant(AssemblyLoadContext context, string variantDir, string? requestedName) {
        if (string.IsNullOrEmpty(requestedName))
            return null;

        var simple = requestedName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(requestedName)
            : new AssemblyName(requestedName).Name;
        if (string.IsNullOrEmpty(simple))
            return null;

        foreach (var asm in context.Assemblies) {
            if (string.Equals(asm.GetName().Name, simple, StringComparison.OrdinalIgnoreCase))
                return asm;
        }

        var path = Path.Combine(variantDir, simple + ".dll");
        if (!File.Exists(path))
            return null;

        return context.LoadFromAssemblyPath(Path.GetFullPath(path));
    }
}
