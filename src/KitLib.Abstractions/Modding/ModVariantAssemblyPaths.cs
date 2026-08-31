namespace KitLib.Abstractions.Modding;

/// <summary>Resolves a bundled implementation DLL under <c>lib/&lt;api&gt;/</c>.</summary>
public static class ModVariantAssemblyPaths {
    public static string? ResolveBundledAssemblyPath(
        string modRoot,
        string assemblyName,
        Version? hostVersion = null) {
        if (string.IsNullOrWhiteSpace(modRoot) || string.IsNullOrWhiteSpace(assemblyName))
            return null;

        var fileName = assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? assemblyName
            : assemblyName + ".dll";

        var variantDir = KitLibHostPaths.TryPickVariantDirectory(modRoot, hostVersion, fileName);
        if (variantDir is null)
            return null;

        var path = Path.Combine(variantDir, fileName);
        return File.Exists(path) ? Path.GetFullPath(path) : null;
    }

    public static string? ResolveSiblingKitLibModDirectory(string hostModDir) =>
        KitLibHostPaths.ResolveSiblingKitLibModDirectory(hostModDir);
}
