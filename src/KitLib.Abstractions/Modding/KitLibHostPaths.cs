namespace KitLib.Abstractions.Modding;

/// <summary>On-disk layout for the KitLib host mod bundle.</summary>
public static class KitLibHostPaths {
    public const string CoreFileName = "KitLib.Core.dll";
    public const string LibDirectoryName = "lib";
    public const string CompatTargetMarkerName = "compat-target.txt";
    public const string ModulesSubdir = "modules";

    /// <summary>When set, satellite modules and content resolve under this variant directory.</summary>
    public static string? ActiveVariantRoot { get; private set; }

    public static void SetActiveVariantRoot(string? variantRoot) {
        ActiveVariantRoot = string.IsNullOrWhiteSpace(variantRoot) ? null : Path.GetFullPath(variantRoot);
    }

    public static string ResolveContentRoot(string modDir) =>
        ActiveVariantRoot ?? modDir;

    public static string ResolveCorePath(string modDir) =>
        Path.Combine(ResolveContentRoot(modDir), CoreFileName);

    public static string ResolveModulesDirectory(string modDir) =>
        Path.Combine(ResolveContentRoot(modDir), ModulesSubdir);

    public static string? TryPickVariantDirectory(string modDir, Version? hostVersion) {
        var libRoot = Path.Combine(modDir, LibDirectoryName);
        if (!Directory.Exists(libRoot))
            return null;

        var bundled = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(libRoot)) {
            var marker = Path.Combine(dir, CompatTargetMarkerName);
            if (!File.Exists(marker))
                continue;

            var label = File.ReadAllText(marker).Trim();
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var core = Path.Combine(dir, CoreFileName);
            if (!File.Exists(core))
                continue;

            bundled.Add(label);
        }

        if (bundled.Count == 0)
            return null;

        var picked = ModVariantPicker.PickCompatTarget(bundled, hostVersion);
        return picked is null ? null : Path.Combine(libRoot, picked);
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
