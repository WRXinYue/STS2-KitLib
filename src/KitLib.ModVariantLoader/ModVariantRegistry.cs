using System.Reflection;

namespace KitLib.ModVariantLoader;

internal static class ModVariantRegistry {
    private static readonly object Sync = new();
    private static readonly List<Assembly> Assemblies = [];

    internal static void Register(Assembly assembly) {
        lock (Sync) {
            if (Assemblies.Any(existing => string.Equals(
                    existing.Location,
                    assembly.Location,
                    StringComparison.OrdinalIgnoreCase)))
                return;
            Assemblies.Add(assembly);
        }
    }

    internal static Type[] GetVariantModTypes() {
        Assembly[] snapshot;
        lock (Sync) {
            snapshot = [.. Assemblies];
        }

        return snapshot.SelectMany(GetLoadableTypes).Distinct().ToArray();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) {
        try {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex) {
            return ex.Types.OfType<Type>();
        }
    }
}
