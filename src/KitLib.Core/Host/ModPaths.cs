using System.Reflection;

namespace KitLib.Host;

internal static class ModPaths {
    internal static string ResolveModRoot(Assembly assembly) {
        var dir = Path.GetDirectoryName(assembly.Location);
        return string.IsNullOrEmpty(dir) ? "" : dir;
    }
}
