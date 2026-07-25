using System.Reflection;
using KitLib.Abstractions.Modding;

namespace KitLib.Host;

internal static class ModPaths {
    internal static string ResolveModRoot(Assembly assembly) {
        var dir = Path.GetDirectoryName(assembly.Location);
        return string.IsNullOrEmpty(dir) ? "" : dir;
    }

    internal static string ResolveContentRoot(Assembly assembly) {
        var modDir = ResolveModRoot(assembly);
        return string.IsNullOrEmpty(modDir) ? "" : KitLibHostPaths.ResolveContentRoot(modDir);
    }
}
