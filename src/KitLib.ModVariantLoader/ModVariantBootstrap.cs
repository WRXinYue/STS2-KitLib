using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using KitLib.Abstractions.Compat;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.ModVariantLoader;

public static class ModVariantBootstrap {
    private static readonly MethodInfo? AssociateAssemblyWithModMethod = CreateAssociateAssemblyWithModMethod();
    private static bool _reflectionBridgePatched;

    public const string ModVariantLoaderAssemblyName = "KitLib.ModVariantLoader";
    public const string KitLibCoreFileName = ModVariantLayout.KitLibHostCoreFileName;
    public const string KitLibModFolderName = "KitLib";
    const string ModManifestFileName = "mod_manifest.json";

    static readonly string[] KitLibHostDeps = [
        "Microsoft.Extensions.Primitives.dll",
        "Semver.dll",
        "KitLib.Abstractions.dll",
        "KitLib.ModVariantLoader.dll",
    ];

    public static void Initialize() => Initialize(null);

    public static void Initialize(ModVariantBootstrapOptions? options) {
        var hostAssembly = Assembly.GetCallingAssembly();
        EnsureHostDependencies(hostAssembly);
        KitLibAssemblyResolver.EnsureHooked(hostAssembly);

        options ??= new ModVariantBootstrapOptions();
        var loaderDir = ResolveLoaderModDirectory(options, hostAssembly, out var earlyLogPrefix);
        if (loaderDir is null)
            return;

        var modId = ResolveModId(options, loaderDir, earlyLogPrefix);
        if (modId is null)
            return;

        var logPrefix = options.LogPrefix ?? $"{modId}.Loader";
        var harmonyId = options.HarmonyId ?? $"KitLib.ModVariantLoader.{modId}";

        LinuxHarmonyNativePreloader.EnsureLoaded(
            message => LogInfo(logPrefix, message),
            message => LogWarn(logPrefix, message));

        var implementationFile = options.ImplementationAssemblyFileName?.Trim();
        if (!string.IsNullOrEmpty(implementationFile)) {
            var flatPath = Path.Combine(loaderDir, implementationFile);
            LoadAndInitializeImplementation(hostAssembly, modId, flatPath, logPrefix, harmonyId);
            return;
        }

        var manifestName = options.VariantManifestFileName ?? ModVariantLayout.ManifestFileName(modId);
        var libRoot = Path.Combine(loaderDir, ModVariantLayout.LibDirectoryName);
        if (!Directory.Exists(libRoot)) {
            LogError(logPrefix, $"Missing lib directory: {libRoot}");
            return;
        }

        var hostNumeric = Sts2HostVersion.Numeric;
        var hostLabel = Sts2HostVersion.ReleaseLabel;
        var picked = PickVariant(loaderDir, libRoot, manifestName, modId, logPrefix, hostNumeric);
        if (picked is null) {
            LogError(
                logPrefix,
                $"No compatible variant under {libRoot} (host={(hostLabel ?? hostNumeric?.ToString()) ?? "unknown"}).");
            return;
        }

        LogInfo(
            logPrefix,
            $"Host version label={hostLabel ?? "<none>"} numeric={hostNumeric?.ToString() ?? "<none>"}; picked variant {picked.CompatTarget}.");

        LoadAndInitializeImplementation(hostAssembly, modId, picked.DllPath, logPrefix, harmonyId);
    }

    static void LoadAndInitializeImplementation(
        Assembly hostAssembly,
        string modId,
        string dllPath,
        string logPrefix,
        string harmonyId) {
        if (!File.Exists(dllPath)) {
            LogError(logPrefix, $"Implementation file missing: {dllPath}");
            return;
        }

        var alc = AssemblyLoadContext.GetLoadContext(hostAssembly) ?? AssemblyLoadContext.Default;
        try {
            var realAsm = alc.LoadFromAssemblyPath(Path.GetFullPath(dllPath));
            ModVariantRegistry.Register(realAsm);
            EnsureReflectionBridgePatch(harmonyId);
            AssociateVariantAssemblyWithGame(modId, realAsm, logPrefix);
            InvokeRealInitializer(realAsm, logPrefix);
        }
        catch (Exception ex) {
            LogError(logPrefix, $"Failed to load {dllPath}: {ex}");
        }
    }

    private static string? ResolveLoaderModDirectory(
        ModVariantBootstrapOptions options,
        Assembly hostAssembly,
        out string logPrefix) {
        logPrefix = options.LogPrefix ?? "ModVariantLoader";

        var configured = options.LoaderModDirectory?.Trim();
        if (!string.IsNullOrEmpty(configured)) {
            var resolved = Path.GetFullPath(configured);
            if (!Directory.Exists(resolved)) {
                LogError(logPrefix, $"Loader mod directory does not exist: {resolved}");
                return null;
            }

            return resolved;
        }

        var hostDir = Path.GetDirectoryName(hostAssembly.Location);
        if (!string.IsNullOrEmpty(hostDir) && Directory.Exists(hostDir))
            return hostDir;

        LogError(logPrefix, $"Could not resolve loader mod directory from host assembly {hostAssembly.FullName}.");
        return null;
    }

    private static string? ResolveModId(
        ModVariantBootstrapOptions options,
        string loaderDir,
        string logPrefix) {
        var configured = options.ModId?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return configured;

        var manifestPath = Path.Combine(loaderDir, ModManifestFileName);
        if (File.Exists(manifestPath)) {
            try {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
                if (doc.RootElement.TryGetProperty("id", out var idElement)) {
                    var id = idElement.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(id))
                        return id;
                }
            }
            catch (Exception ex) {
                LogWarn(logPrefix, $"Could not read mod id from {manifestPath}: {ex.Message}");
            }
        }

        var folderName = Path.GetFileName(loaderDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.IsNullOrEmpty(folderName))
            return folderName;

        LogError(logPrefix, $"Could not resolve mod id under {loaderDir}. Add mod_manifest.json or pass ModId.");
        return null;
    }

    private static void EnsureHostDependencies(Assembly hostAssembly) {
        var alc = AssemblyLoadContext.GetLoadContext(hostAssembly) ?? AssemblyLoadContext.Default;

        var hostDir = Path.GetDirectoryName(hostAssembly.Location);
        if (string.IsNullOrEmpty(hostDir))
            throw new InvalidOperationException("Mod variant loader: cannot resolve host mod directory.");

        var hostIsKitLibLoader = string.Equals(hostAssembly.GetName().Name, KitLibModFolderName, StringComparison.OrdinalIgnoreCase);
        var hostIsModVariantLoader = string.Equals(
            hostAssembly.GetName().Name,
            ModVariantLoaderAssemblyName,
            StringComparison.OrdinalIgnoreCase);

        var kitLibDir = ResolveKitLibInstallDirectory(hostDir, hostIsKitLibLoader);

        PreloadKitLibImplementation(alc, kitLibDir, hostIsKitLibLoader);

        foreach (var fileName in KitLibHostDeps) {
            var simpleName = Path.GetFileNameWithoutExtension(fileName);
            if (FindLoaded(alc, simpleName) != null)
                continue;
            if (hostIsModVariantLoader &&
                string.Equals(simpleName, ModVariantLoaderAssemblyName, StringComparison.OrdinalIgnoreCase))
                continue;

            var path = Path.Combine(kitLibDir, fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Missing {fileName} under KitLib install folder ({kitLibDir}). Update KitLib to 0.24+.",
                    path);

            alc.LoadFromAssemblyPath(path);
        }

        if (FindLoaded(alc, ModVariantLoaderAssemblyName) is null)
            throw new FileNotFoundException(
                $"KitLib.ModVariantLoader failed to load from KitLib install folder ({kitLibDir}).");
    }

    private static string ResolveKitLibInstallDirectory(string hostDir, bool hostIsKitLibLoader) {
        if (hostIsKitLibLoader)
            return hostDir;

        if (TryResolveKitLibDirectoryFromGame(out var fromGame))
            return fromGame;

        var legacySibling = Path.GetFullPath(Path.Combine(hostDir, "..", KitLibModFolderName));
        if (Directory.Exists(legacySibling))
            return legacySibling;

        throw new DirectoryNotFoundException(
            $"KitLib install folder not found. Enable KitLib in mod settings (Steam Workshop or mods/{KitLibModFolderName}/).");
    }

    private static bool TryResolveKitLibDirectoryFromGame(out string kitLibDir) {
        kitLibDir = "";

        foreach (var mod in ModManager.GetLoadedMods()) {
            if (!string.Equals(mod.manifest?.id, KitLibModFolderName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryNormalizeExistingDirectory(mod.path, out kitLibDir))
                return true;

            if (TryNormalizeExistingDirectory(TryReadFirstAssemblyLocation(mod), out kitLibDir))
                return true;
        }

        foreach (var mod in ModManager.Mods) {
            if (!string.Equals(ReadManifestId(mod), KitLibModFolderName, StringComparison.Ordinal))
                continue;

            if (TryNormalizeExistingDirectory(mod.path, out kitLibDir))
                return true;
        }

        return false;
    }

    private static string? TryReadFirstAssemblyLocation(Mod mod) {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var modType = mod.GetType();

        foreach (var memberName in new[] { "assembly", "assemblies" }) {
            object? value = modType.GetField(memberName, flags)?.GetValue(mod)
                ?? modType.GetProperty(memberName, flags)?.GetValue(mod);
            if (value is Assembly assembly) {
                if (!string.IsNullOrWhiteSpace(assembly.Location))
                    return assembly.Location;
                continue;
            }

            if (value is not IEnumerable assemblies)
                continue;

            foreach (var item in assemblies) {
                if (item is not Assembly loaded || string.IsNullOrWhiteSpace(loaded.Location))
                    continue;

                return loaded.Location;
            }
        }

        return null;
    }

    private static bool TryNormalizeExistingDirectory(string? path, out string directory) {
        directory = "";
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var resolved = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        if (string.IsNullOrEmpty(resolved))
            return false;

        resolved = Path.GetFullPath(resolved);
        if (!Directory.Exists(resolved))
            return false;

        directory = resolved;
        return true;
    }

    static void PreloadKitLibImplementation(AssemblyLoadContext alc, string kitLibDir, bool hostIsKitLibLoader) {
        if (hostIsKitLibLoader)
            return;

        var corePath = Path.Combine(kitLibDir, KitLibCoreFileName);
        if (File.Exists(corePath)) {
            if (FindLoaded(alc, "KitLib.Core") is null)
                alc.LoadFromAssemblyPath(Path.GetFullPath(corePath));
            return;
        }

        var legacyPath = Path.Combine(kitLibDir, "KitLib.dll");
        if (File.Exists(legacyPath) && FindLoaded(alc, KitLibModFolderName) is null)
            alc.LoadFromAssemblyPath(Path.GetFullPath(legacyPath));
    }

    private static Assembly? FindLoaded(AssemblyLoadContext alc, string simpleName) {
        foreach (var asm in alc.Assemblies) {
            if (string.Equals(asm.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                return asm;
        }

        return null;
    }

    private static void EnsureReflectionBridgePatch(string harmonyId) {
        if (_reflectionBridgePatched)
            return;

        var harmony = new Harmony(harmonyId);
        harmony.PatchAll(typeof(ModVariantBootstrap).Assembly);
        _reflectionBridgePatched = true;
    }

    private static void AssociateVariantAssemblyWithGame(string modId, Assembly assembly, string logPrefix) {
        if (AssociateAssemblyWithModMethod != null)
            try {
                AssociateAssemblyWithModMethod.Invoke(null, [modId, assembly]);
                if (IsAssemblyAssociatedWithMod(modId, assembly))
                    return;

                LogWarn(
                    logPrefix,
                    $"Host AssociateAssemblyWithMod did not record variant assembly {assembly.FullName} for {modId}; applying initializer fallback.");
            }
            catch (Exception ex) {
                LogWarn(
                    logPrefix,
                    $"Failed to associate variant assembly {assembly.FullName} with {modId}: {ex.Message}");
            }

        if (TryAssociateAssemblyWithModList(modId, assembly, logPrefix))
            return;

        LogWarn(
            logPrefix,
            $"Could not associate variant assembly {assembly.FullName} with {modId}; relying on reflection bridge for type discovery.");
    }

    private static bool IsAssemblyAssociatedWithMod(string modId, Assembly assembly) =>
        TryFindMod(modId, out var mod) &&
        TryGetMutableAssembliesList(mod, out var assemblies) &&
        ContainsAssembly(assemblies, assembly);

    private static bool TryAssociateAssemblyWithModList(string modId, Assembly assembly, string logPrefix) {
        if (!TryFindMod(modId, out var mod))
            return false;

        if (!TryGetMutableAssembliesList(mod, out var assemblies))
            return false;

        if (!ContainsAssembly(assemblies, assembly)) {
            assemblies.Add(assembly);
            LogInfo(
                logPrefix,
                $"Associated variant assembly {assembly.FullName} with {modId} during initialization.");
        }

        return true;
    }

    private static bool TryFindMod(string modId, out Mod mod) {
        foreach (var candidate in ModManager.Mods) {
            if (!string.Equals(ReadManifestId(candidate), modId, StringComparison.Ordinal))
                continue;

            mod = candidate;
            return true;
        }

        mod = null!;
        return false;
    }

    private static string? ReadManifestId(Mod mod) {
        var manifest = typeof(Mod)
            .GetField("manifest", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(mod);
        return manifest?.GetType()
            .GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(manifest) as string;
    }

    private static bool TryGetMutableAssembliesList(Mod mod, out IList assemblies) {
        assemblies = null!;
        var value = typeof(Mod).GetField("assemblies",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(mod);
        if (value is not IList list)
            return false;

        assemblies = list;
        return true;
    }

    private static bool ContainsAssembly(IEnumerable assemblies, Assembly assembly) =>
        assemblies.Cast<object?>().Any(item => ReferenceEquals(item, assembly));

    private static MethodInfo? CreateAssociateAssemblyWithModMethod() =>
        typeof(ModManager).GetMethod(
            "AssociateAssemblyWithMod",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(string), typeof(Assembly)],
            null);

    private static void InvokeRealInitializer(Assembly realAsm, string logPrefix) {
        Type[] types;
        try {
            types = realAsm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex) {
            LogError(logPrefix, $"ReflectionTypeLoadException while scanning {realAsm.FullName}: {ex}");
            if (ex.Types is null)
                return;
            foreach (var t in ex.Types.Where(static x => x is not null))
                TryInvokeInitializerOnType(t!, logPrefix);

            return;
        }

        if (types.Any(t => TryInvokeInitializerOnType(t, logPrefix)))
            return;

        LogError(logPrefix, $"No type with {nameof(ModInitializerAttribute)} found in {realAsm.FullName}.");
    }

    private static bool TryInvokeInitializerOnType(Type t, string logPrefix) {
        var attr = t.GetCustomAttribute<ModInitializerAttribute>();
        if (attr is null)
            return false;

        var method = t.GetMethod(attr.initializerMethod,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method is null) {
            LogError(
                logPrefix,
                $"Type {t.FullName} has {nameof(ModInitializerAttribute)} but no static method {attr.initializerMethod}.");
            return false;
        }

        method.Invoke(null, null);
        return true;
    }

    private static VariantCandidate? PickVariant(
        string loaderDir,
        string libRoot,
        string manifestName,
        string modId,
        string logPrefix,
        Version? host) {
        var variants = LoadVariantManifest(loaderDir, libRoot, manifestName, modId, logPrefix);
        if (variants.Count == 0)
            return null;

        variants.Sort(static (a, b) => a.Version.CompareTo(b.Version));

        if (host is null) {
            LogInfo(logPrefix, "Host numeric version unknown; using newest bundled variant.");
            return variants[^1];
        }

        var candidates = variants.Where(x => x.Version <= host).ToList();
        if (candidates.Count > 0)
            return candidates[^1];

        LogInfo(
            logPrefix,
            $"No bundled variant <= host {host}; using newest bundled variant as best-effort fallback.");
        return variants[^1];
    }

    private static List<VariantCandidate> LoadVariantManifest(
        string loaderDir,
        string libRoot,
        string manifestName,
        string modId,
        string logPrefix) {
        var manifestPath = Path.Combine(loaderDir, manifestName);
        if (!File.Exists(manifestPath)) {
            LogError(logPrefix, $"Missing variant manifest: {manifestPath}");
            return [];
        }

        ModVariantManifestFile manifest;
        try {
            manifest = ModVariantManifestIO.Read(manifestPath);
        }
        catch (Exception ex) {
            LogError(logPrefix, $"Failed to read variant manifest {manifestPath}: {ex}");
            return [];
        }

        if (manifest.Variants.Count == 0) {
            LogError(logPrefix, $"Variant manifest contains no variants: {manifestPath}");
            return [];
        }

        var libRootFull = Path.GetFullPath(libRoot);

        return manifest.Variants
            .Select(entry => TryCreateVariantCandidate(loaderDir, libRootFull, entry, modId, logPrefix))
            .OfType<VariantCandidate>()
            .ToList();
    }

    private static VariantCandidate? TryCreateVariantCandidate(
        string loaderDir,
        string libRootFull,
        ModVariantEntry entry,
        string modId,
        string logPrefix) {
        var compatTarget = entry.CompatTarget?.Trim();
        if (string.IsNullOrWhiteSpace(compatTarget) ||
            !Sts2GameVersion.TryParseCore(compatTarget, out var version)) {
            LogError(logPrefix, $"Ignoring invalid variant target '{entry.CompatTarget}'.");
            return null;
        }

        var relativeFile = string.IsNullOrWhiteSpace(entry.File)
            ? ModVariantLayout.VariantRelativePath(modId, compatTarget)
            : entry.File.Trim().Replace('\\', '/');
        var expectedFile = ModVariantLayout.VariantRelativePath(modId, compatTarget);
        if (!string.Equals(relativeFile, expectedFile, StringComparison.OrdinalIgnoreCase)) {
            LogError(logPrefix, $"Ignoring variant with unexpected file path: {relativeFile}");
            return null;
        }

        var dllPath = Path.GetFullPath(Path.Combine(loaderDir, relativeFile.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsUnderDirectory(dllPath, libRootFull)) {
            LogError(logPrefix, $"Ignoring variant outside lib directory: {relativeFile}");
            return null;
        }

        if (!File.Exists(dllPath)) {
            LogError(logPrefix, $"Ignoring missing variant file: {dllPath}");
            return null;
        }

        var fileName = Path.GetFileName(dllPath);
        if (!ModVariantLayout.TryParseVariantFileName(modId, fileName, out var parsedTarget) ||
            !string.Equals(parsedTarget, compatTarget, StringComparison.OrdinalIgnoreCase)) {
            LogError(logPrefix, $"Ignoring variant with mismatched file name: {fileName}");
            return null;
        }

        if (MatchesExpectedHash(dllPath, entry.Sha256))
            return new(compatTarget, version, dllPath);

        LogError(logPrefix, $"Ignoring variant with mismatched hash: {dllPath}");
        return null;
    }

    private static bool IsUnderDirectory(string path, string root) {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                             Path.DirectorySeparatorChar;
        var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                               Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesExpectedHash(string path, string? expectedSha256) {
        if (string.IsNullOrWhiteSpace(expectedSha256))
            return false;

        var actual = ModVariantManifestIO.ComputeSha256Hex(path);
        return string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static void LogInfo(string prefix, string message) => Log.Info($"[{prefix}] {message}");

    private static void LogWarn(string prefix, string message) => Log.Warn($"[{prefix}] {message}");

    private static void LogError(string prefix, string message) => Log.Error($"[{prefix}] {message}");

    private sealed record VariantCandidate(string CompatTarget, Version Version, string DllPath);
}
