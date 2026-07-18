namespace KitLib.Abstractions.Modding;

/// <summary>On-disk layout for the KitLib host mod bundle.</summary>
public static class KitLibHostPaths {
    public const string CoreFileName = "KitLib.Core.dll";

    public static string? ResolveSiblingKitLibModDirectory(string hostModDir) {
        if (string.IsNullOrWhiteSpace(hostModDir))
            return null;

        if (string.Equals(Path.GetFileName(hostModDir), "KitLib", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(hostModDir);

        var sibling = Path.GetFullPath(Path.Combine(hostModDir, "..", "KitLib"));
        return Directory.Exists(sibling) ? sibling : null;
    }
}
