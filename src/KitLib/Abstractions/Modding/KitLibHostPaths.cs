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

    /// <summary>
    /// Core registers this from <c>ModManager.Mods</c>. Not for content mods.
    /// </summary>
    internal static Func<string, string?>? GameModDirectoryResolver { get; set; }

    /// <summary>Test hook: extra <c>mods/</c> roots (named product folders) besides KitLib's parent.</summary>
    internal static IReadOnlyList<string>? AdditionalModsSearchRoots { get; set; }

    public static void SetActiveVariantRoot(string? variantRoot) {
        ActiveVariantRoot = string.IsNullOrWhiteSpace(variantRoot) ? null : Path.GetFullPath(variantRoot);
    }

    public static string ResolveContentRoot(string modDir) {
        if (!string.IsNullOrEmpty(ActiveVariantRoot))
            return ActiveVariantRoot;

        var folder = ResolveModFolder(modDir);
        return TryPickVariantDirectory(folder, hostVersion: null) ?? folder;
    }

    /// <summary>
    /// <c>lib/&lt;api&gt;/KitLib.Core.dll</c> for this install. Does not fall back to
    /// <c>KitLib.dll</c> or a root-level Core DLL.
    /// </summary>
    public static string? TryResolveCoreAssemblyPath(string kitLibModDir, Version? hostVersion = null) {
        var folder = ResolveModFolder(kitLibModDir);
        if (!string.IsNullOrEmpty(ActiveVariantRoot)) {
            var active = Path.Combine(ActiveVariantRoot, CoreFileName);
            if (File.Exists(active))
                return Path.GetFullPath(active);
        }

        var picked = TryPickVariantDirectory(folder, hostVersion);
        if (picked is null)
            return null;

        var core = Path.Combine(picked, CoreFileName);
        return File.Exists(core) ? Path.GetFullPath(core) : null;
    }

    public static string ResolveModulesDirectory(string modDir) =>
        Path.Combine(ResolveContentRoot(modDir), ModulesSubdir);

    /// <summary>
    /// Walk from a DLL directory up to the Workshop/mod folder (past <c>modules/</c>
    /// and <c>lib/&lt;api&gt;/</c>).
    /// </summary>
    public static string ResolveModFolder(string directory) {
        if (string.IsNullOrWhiteSpace(directory))
            return directory;

        var dir = Path.GetFullPath(directory);
        if (string.Equals(Path.GetFileName(dir), ModulesSubdir, StringComparison.OrdinalIgnoreCase)) {
            var parent = Path.GetDirectoryName(dir);
            if (!string.IsNullOrEmpty(parent))
                dir = parent;
        }

        if (File.Exists(Path.Combine(dir, CompatTargetMarkerName))) {
            var apiParent = Path.GetDirectoryName(dir);
            if (!string.IsNullOrEmpty(apiParent) &&
                string.Equals(Path.GetFileName(apiParent), LibDirectoryName, StringComparison.OrdinalIgnoreCase)) {
                var modRoot = Path.GetDirectoryName(apiParent);
                if (!string.IsNullOrEmpty(modRoot))
                    return modRoot;
            }
        }

        return dir;
    }

    /// <summary>Game <c>mods/</c> directory that contains KitLib and sibling products.</summary>
    public static string? ResolveModsRoot(string kitLibModDir) {
        if (string.IsNullOrWhiteSpace(kitLibModDir))
            return null;
        var parent = Path.GetDirectoryName(ResolveModFolder(kitLibModDir));
        return string.IsNullOrEmpty(parent) ? null : parent;
    }

    /// <summary>
    /// Directories searched for satellite DLLs: KitLib content root modules, then each
    /// installed sibling product. When a KitLib <c>lib/&lt;api&gt;</c> variant is active,
    /// sibling products use that same API folder before unversioned fallbacks.
    /// </summary>
    public static IReadOnlyList<string> EnumerateModuleSearchDirectories(string kitLibModDir) {
        var dirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        kitLibModDir = ResolveModFolder(kitLibModDir);

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

        foreach (var productId in KitLibProductIds.All) {
            if (string.Equals(productId, KitLibProductIds.KitLib, StringComparison.OrdinalIgnoreCase))
                continue;

            var productDir = TryResolveProductDirectory(kitLibModDir, productId);
            if (productDir is null)
                continue;

            AddSiblingProductDirectories(productDir, Add);
        }

        return dirs;
    }

    /// <summary>
    /// Locate an installed product by official <c>Mod.manifest.id</c> and <c>Mod.path</c>
    /// (<see cref="ModManager"/>), then named folders under local <c>mods/</c>.
    /// Workshop items live in numeric Steam folders, not <c>mods/KitDevTools</c>.
    /// </summary>
    public static string? TryResolveProductDirectory(string kitLibModDir, string productId) {
        if (string.IsNullOrWhiteSpace(productId))
            return null;

        var fromGame = GameModDirectoryResolver?.Invoke(productId);
        if (!string.IsNullOrWhiteSpace(fromGame) && Directory.Exists(fromGame))
            return Path.GetFullPath(fromGame);

        foreach (var modsRoot in EnumerateNamedModsRoots(kitLibModDir)) {
            var candidate = Path.Combine(modsRoot, productId);
            if (Directory.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    static IEnumerable<string> EnumerateNamedModsRoots(string kitLibModDir) {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<string>();

        void AddRoot(string? dir) {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return;
            var full = Path.GetFullPath(dir);
            if (seen.Add(full))
                roots.Add(full);
        }

        AddRoot(ResolveModsRoot(kitLibModDir));
        foreach (var official in EnumerateOfficialGameModsRoots())
            AddRoot(official);
        if (AdditionalModsSearchRoots != null) {
            foreach (var extra in AdditionalModsSearchRoots)
                AddRoot(extra);
        }

        return roots;
    }

    /// <summary>
    /// Official scan roots from <c>ModManager.Initialize</c>: <c>mods/</c> and
    /// <c>mods_STEAMTEST/</c> next to the game executable.
    /// </summary>
    static IEnumerable<string> EnumerateOfficialGameModsRoots() {
        string? exeDir = null;
        try {
            var process = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(process))
                exeDir = Path.GetDirectoryName(process);
        }
        catch (Exception) {
            exeDir = null;
        }

        if (string.IsNullOrEmpty(exeDir))
            yield break;

        yield return Path.Combine(exeDir, "mods");
        yield return Path.Combine(exeDir, "mods_STEAMTEST");
    }

    /// <summary>
    /// Prefer the sibling <c>lib/&lt;api&gt;</c> that matches the already-picked KitLib
    /// variant. Loading a beta satellite onto a stable game (or the reverse) applies
    /// Harmony patches that JIT-fail and abort vanilla methods used by other mods.
    /// </summary>
    static void AddSiblingProductDirectories(string productDir, Action<string?> add) {
        var pickedApi = TryGetActiveCompatTarget();
        if (pickedApi is null) {
            var kitLibDir = ResolveSiblingKitLibModDirectory(productDir) ?? ResolveModFolder(productDir);
            var picked = TryPickVariantDirectory(kitLibDir, hostVersion: null);
            if (picked is not null)
                pickedApi = Path.GetFileName(picked);
        }

        if (pickedApi is null)
            return;

        var variantDir = Path.Combine(productDir, LibDirectoryName, pickedApi);
        if (!File.Exists(Path.Combine(variantDir, CompatTargetMarkerName)))
            return;

        add(Path.Combine(variantDir, ModulesSubdir));
        add(variantDir);
    }

    static string? TryGetActiveCompatTarget() {
        var root = ActiveVariantRoot;
        if (string.IsNullOrEmpty(root))
            return null;

        var marker = Path.Combine(root, CompatTargetMarkerName);
        if (File.Exists(marker)) {
            var label = File.ReadAllText(marker).Trim();
            if (!string.IsNullOrWhiteSpace(label))
                return label;
        }

        var folder = Path.GetFileName(root);
        return string.IsNullOrWhiteSpace(folder) ? null : folder;
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

    public static bool IsSiblingProductInstalled(string kitLibModDir, string productId) =>
        TryResolveProductDirectory(kitLibModDir, productId) is not null;

    /// <param name="requiredFileName">
    /// File that must exist in a variant directory. KitLib uses <see cref="CoreFileName"/>;
    /// content mods pass <c>&lt;ModId&gt;.dll</c>. Empty skips the file check.
    /// </param>
    public static string? TryPickVariantDirectory(
        string modDir,
        Version? hostVersion,
        string requiredFileName = CoreFileName) {
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

            if (!string.IsNullOrEmpty(requiredFileName) &&
                !File.Exists(Path.Combine(dir, requiredFileName)))
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

        var folder = ResolveModFolder(hostModDir);
        if (string.Equals(Path.GetFileName(folder), "KitLib", StringComparison.OrdinalIgnoreCase))
            return folder;

        var sibling = Path.GetFullPath(Path.Combine(folder, "..", "KitLib"));
        return Directory.Exists(sibling) ? sibling : null;
    }
}
