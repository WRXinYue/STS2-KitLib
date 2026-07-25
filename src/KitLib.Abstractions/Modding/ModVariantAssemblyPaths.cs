using KitLib.Abstractions.Compat;

namespace KitLib.Abstractions.Modding;

/// <summary>Shared disk layout helpers for variant bundles (content mods and optional profile builds).</summary>
public static class ModVariantAssemblyPaths {
    public static string? ResolveBundledAssemblyPath(
        string modRoot,
        string assemblyName,
        Version? hostVersion = null) {
        if (string.IsNullOrWhiteSpace(modRoot) || string.IsNullOrWhiteSpace(assemblyName))
            return null;

        var libDir = Path.Combine(modRoot, ModVariantLayout.LibDirectoryName);
        if (!Directory.Exists(libDir))
            return null;

        var bundled = new List<string>();
        foreach (var file in Directory.EnumerateFiles(libDir, $"{assemblyName}_*.dll")) {
            var fileName = Path.GetFileName(file);
            if (ModVariantLayout.TryParseVariantFileName(assemblyName, fileName, out var target))
                bundled.Add(target);
        }

        if (bundled.Count == 0)
            return null;

        var picked = ModVariantPicker.PickCompatTarget(bundled, hostVersion);
        if (picked is null)
            return null;

        var path = Path.Combine(libDir, ModVariantLayout.VariantFileName(assemblyName, picked));
        return File.Exists(path) ? Path.GetFullPath(path) : null;
    }

    public static string? ResolveSiblingKitLibModDirectory(string hostModDir) {
        if (string.IsNullOrWhiteSpace(hostModDir))
            return null;

        if (string.Equals(Path.GetFileName(hostModDir), "KitLib", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(hostModDir);

        var sibling = Path.GetFullPath(Path.Combine(hostModDir, "..", "KitLib"));
        return Directory.Exists(sibling) ? sibling : null;
    }
}
