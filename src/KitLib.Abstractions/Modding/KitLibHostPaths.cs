using KitLib.Abstractions.Host;

namespace KitLib.Abstractions.Modding;

/// <summary>On-disk layout for the KitLib host mod bundle and sibling products.</summary>
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

    /// <summary>Game <c>mods/</c> directory that contains KitLib and sibling products.</summary>
    public static string? ResolveModsRoot(string kitLibModDir) {
        if (string.IsNullOrWhiteSpace(kitLibModDir))
            return null;
        var parent = Path.GetDirectoryName(Path.GetFullPath(kitLibModDir));
        return string.IsNullOrEmpty(parent) ? null : parent;
    }

    /// <summary>
    /// Directories searched for satellite DLLs: KitLib content root modules, then each
    /// installed sibling product <c>modules/</c> (and product root for thin loaders).
    /// </summary>
    public static IReadOnlyList<string> EnumerateModuleSearchDirectories(string kitLibModDir) {
        var dirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? dir) {
            if (string.IsNullOrWhiteSpace(dir))
                return;
            var full = Path.GetFullPath(dir);
            if (!Directory.Exists(full) || !seen.Add(full))
                return;
            dirs.Add(full);
        }

        Add(ResolveModulesDirectory(kitLibModDir));
        Add(ResolveContentRoot(kitLibModDir));

        var modsRoot = ResolveModsRoot(kitLibModDir);
        if (modsRoot is null)
            return dirs;

        foreach (var productId in KitLibProductIds.All) {
            if (string.Equals(productId, KitLibProductIds.KitLib, StringComparison.OrdinalIgnoreCase))
                continue;

            var productDir = Path.Combine(modsRoot, productId);
            if (!Directory.Exists(productDir))
                continue;

            Add(Path.Combine(productDir, ModulesSubdir));
            Add(productDir);

            var libRoot = Path.Combine(productDir, LibDirectoryName);
            if (!Directory.Exists(libRoot))
                continue;

            foreach (var variantDir in Directory.EnumerateDirectories(libRoot)) {
                var marker = Path.Combine(variantDir, CompatTargetMarkerName);
                if (!File.Exists(marker))
                    continue;
                Add(Path.Combine(variantDir, ModulesSubdir));
                Add(variantDir);
            }
        }

        return dirs;
    }

    public static string? TryResolveSatelliteAssemblyPath(string kitLibModDir, string assemblyName) {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return null;

        var fileName = assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? assemblyName
            : assemblyName + ".dll";

        foreach (var dir in EnumerateModuleSearchDirectories(kitLibModDir)) {
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        return null;
    }

    public static bool IsSiblingProductInstalled(string kitLibModDir, string productId) {
        var modsRoot = ResolveModsRoot(kitLibModDir);
        if (modsRoot is null || string.IsNullOrWhiteSpace(productId))
            return false;
        return Directory.Exists(Path.Combine(modsRoot, productId));
    }

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
