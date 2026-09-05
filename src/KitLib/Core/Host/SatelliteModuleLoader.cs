using System.Reflection;
using KitLib.Abstractions.Compat;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using KitLib.Diagnostics;

namespace KitLib.Host;

/// <summary>
/// Loads satellite DLLs from sibling product folders (KitDevTools).
/// Skips modules that are missing, have unmet prerequisites, or fail to init.
/// </summary>
internal static class SatelliteModuleLoader {
    internal const string ModulesSubdir = KitLibHostPaths.ModulesSubdir;
    private static Assembly? _devAssembly;

    sealed record ModuleSpec(
        string ModuleId,
        string AssemblyName,
        string? EntryTypeName,
        string[] Requires,
        bool SettingsControlled = true);

    static readonly ModuleSpec[] LoadOrder = [
        new(ModuleIds.Panel, "KitLib.Panel", "KitLib.PanelMod.ModuleEntry", []),
        new(ModuleIds.Dev, "KitLib.Dev", "KitLib.Dev.ModuleEntry", [ModuleIds.Panel]),
    ];

    internal static void LoadBundledModules() {
        var modDir = ModPaths.ResolveModRoot(typeof(MainFile).Assembly);
        if (string.IsNullOrEmpty(modDir)) {
            MainFile.Logger.Warn("Satellite loader: cannot resolve mod directory.");
            return;
        }

        var searchDirs = KitLibHostPaths.EnumerateModuleSearchDirectories(modDir);
        ModAssemblyLoader.EnsureResolveHook(modDir, searchDirs);
        MainFile.Logger.Info(
            $"Satellite loader: modDir={modDir}; searchDirs={searchDirs.Count}.");

        KitLibStartupAudit.Measure("satellite.preload", () => PreloadBundledAssemblies(modDir));

        var loaded = new List<string>();
        foreach (var spec in LoadOrder) {
            MainFile.Logger.Info($"Satellite loader: trying {spec.ModuleId} ({spec.AssemblyName}.dll).");
            if (TryLoadModule(modDir, spec))
                loaded.Add(spec.ModuleId);
        }

        if (ModuleCatalog.IsLoaded(ModuleIds.Dev)) {
            var devAssembly = _devAssembly ?? ModAssemblyLoader.GetLoadedAssembly("KitLib.Dev");
            KitLibStartupAudit.Measure("satellite.devWire", () => WireDevModuleDelegates(devAssembly));
            KitLibStartupAudit.Measure("satellite.devHarmony", () => ApplyDevHarmony(devAssembly));
        }

        if (loaded.Count == 0)
            MainFile.Logger.Info("Satellite loader done: no bundled modules loaded.");
        else
            MainFile.Logger.Info($"Satellite loader done: loaded {loaded.Count} — {string.Join(", ", loaded)}.");
    }

    static void PreloadBundledAssemblies(string modDir) {
        foreach (var spec in LoadOrder)
            TryPreloadAssembly(modDir, spec.AssemblyName);
    }

    static void TryPreloadAssembly(string modDir, string assemblyName) {
        var path = ResolveSatelliteAssemblyPath(modDir, assemblyName);
        if (path is null)
            return;

        try {
            ModAssemblyLoader.LoadFromModPath(path);
        }
        catch (Exception ex) {
            if (Sts2RuntimeProfile.Platform == Sts2Platform.Android)
                KitLog.Debug($"Preload {assemblyName} failed — {ex.Message}");
            else
                KitLog.Warn($"Preload {assemblyName} failed — {ex.Message}");
        }
    }

    static void LogModuleSkip(string moduleId, string reason) {
        var message = $"Module {moduleId} skipped — {reason}";
        if (Sts2RuntimeProfile.Platform == Sts2Platform.Android)
            KitLog.Debug(message);
        else
            KitLog.Warn(message);
    }

    static void LogModuleFailure(string moduleId, string reason) {
        var message = $"Module {moduleId} init failed — skipped ({reason})";
        if (Sts2RuntimeProfile.Platform == Sts2Platform.Android)
            KitLog.Debug(message);
        else
            KitLog.Warn(message);
    }

    static bool TryLoadModule(string modDir, ModuleSpec spec) {
        if (!Sts2RuntimeProfile.AllowHighRiskModules && spec.ModuleId == ModuleIds.Dev) {
            LogModuleSkip(spec.ModuleId, $"unsupported STS2 version {Sts2RuntimeProfile.RawVersion ?? "?"}.");
            return false;
        }

        if (ModuleCatalog.IsLoaded(spec.ModuleId)) {
            KitLog.Info($"Module {spec.ModuleId} already active — skipping load.");
            return true;
        }

        var dllExists = ModuleAssemblyExists(modDir, spec.AssemblyName);
        if (!dllExists) {
            var productId = KitLibProductIds.TryGetProductIdForModule(spec.ModuleId);
            KitLog.Info(
                productId is null
                    ? $"Module {spec.ModuleId} not present ({spec.AssemblyName}.dll)."
                    : $"Module {spec.ModuleId} not present ({spec.AssemblyName}.dll) — install product {productId} to enable.");
            return false;
        }

        foreach (var required in spec.Requires) {
            if (!ModuleCatalog.IsLoaded(required)) {
                LogModuleSkip(spec.ModuleId, $"prerequisite {required} is not loaded.");
                return false;
            }
        }

        try {
            return KitLibStartupAudit.Measure($"satellite.{spec.ModuleId}", () => LoadModuleCore(modDir, spec));
        }
        catch (TargetInvocationException ex) {
            LogModuleFailure(spec.ModuleId, ex.InnerException?.Message ?? ex.Message);
            return false;
        }
        catch (Exception ex) {
            LogModuleFailure(spec.ModuleId, ex.Message);
            return false;
        }
    }

    static bool LoadModuleCore(string modDir, ModuleSpec spec) {
        try {
            KitLog.Info($"Satellite loader: loading {spec.AssemblyName}.dll assembly file.");
            var assembly = LoadAssembly(modDir, spec.AssemblyName, spec.ModuleId);
            if (assembly == null)
                return false;
            AssociateSatelliteAssembly(assembly);
            KitLog.Info($"Satellite loader: {spec.AssemblyName} assembly loaded.");

            if (spec.EntryTypeName == null) {
                ModuleCatalog.Announce(spec.ModuleId);
                KitLog.Info($"Loaded passive module {spec.ModuleId}.");
                return true;
            }

            KitLog.Info($"Satellite loader: resolving entry {spec.EntryTypeName}.");
            var entryType = assembly.GetType(spec.EntryTypeName, throwOnError: false);
            if (entryType == null) {
                LogModuleSkip(spec.ModuleId, $"entry type {spec.EntryTypeName} not found.");
                return false;
            }

            var init = entryType.GetMethod(
                "Initialize",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            if (init == null) {
                LogModuleSkip(spec.ModuleId, "Initialize() not found.");
                return false;
            }

            InvokeModuleInitialize(spec.ModuleId, init);

            if (spec.ModuleId == ModuleIds.Dev)
                _devAssembly = assembly;

            if (!ModuleCatalog.IsLoaded(spec.ModuleId))
                ModuleCatalog.Announce(spec.ModuleId);
            return ModuleCatalog.IsLoaded(spec.ModuleId);
        }
        catch (TargetInvocationException ex) {
            LogModuleFailure(spec.ModuleId, ex.InnerException?.Message ?? ex.Message);
            return false;
        }
        catch (Exception ex) {
            LogModuleFailure(spec.ModuleId, ex.Message);
            return false;
        }
    }

    static void WireDevModuleDelegates(Assembly? devAssembly) {
        if (devAssembly == null) {
            KitLog.Warn($"KitLib.Dev assembly not resolved — Dev runtime wiring skipped.");
            return;
        }

        var bootstrap = devAssembly.GetType("KitLib.Dev.ModuleBootstrap", throwOnError: false);
        if (bootstrap == null) {
            KitLog.Warn($"KitLib.Dev.ModuleBootstrap not found.");
            return;
        }

        var complete = bootstrap.GetMethod(
            "Complete",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        if (complete != null)
            KitLibHost.RequestDevBootstrap = () => complete.Invoke(null, null);

        KitLibHost.EnsureDevHarmonyApplied = () => ApplyDevHarmony(devAssembly);

        var adopt = bootstrap.GetMethod(
            "AdoptPinnedModDataDir",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (adopt != null) {
            if (!DataPaths.TryGetPinnedBaseDir(out var modDataDir))
                modDataDir = KitLibHost.ModDataDir;
            adopt.Invoke(null, [modDataDir]);
        }

        DataPaths.TryGetPinnedBaseDir(out var wiredDir);
    }

    static void ApplyDevHarmony(Assembly? devAssembly) {
        if (devAssembly == null)
            return;
        KitLibHarmony.Apply(devAssembly, ModuleIds.Dev);
        MarkDevHarmonyAppliedOnBootstrap(devAssembly);
    }

    static void MarkDevHarmonyAppliedOnBootstrap(Assembly devAssembly) {
        var bootstrap = devAssembly.GetType("KitLib.Dev.ModuleBootstrap", throwOnError: false);
        var mark = bootstrap?.GetMethod(
            "MarkHarmonyAppliedByHost",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        mark?.Invoke(null, null);
    }

    static void InvokeModuleInitialize(string moduleId, MethodInfo init) {
        try {
            init.Invoke(null, null);
        }
        catch (TargetInvocationException ex) {
            LogModuleFailure(moduleId, ex.InnerException?.Message ?? ex.Message);
        }
        catch (Exception ex) {
            LogModuleFailure(moduleId, ex.Message);
        }
    }

    internal static bool IsSatelliteDllPresent(string moduleId) {
        if (!SatelliteModuleLoadPolicy.IsKnownSatellite(moduleId))
            return false;
        var modDir = ModPaths.ResolveModRoot(typeof(MainFile).Assembly);
        return !string.IsNullOrEmpty(modDir) && ModuleAssemblyExists(modDir, moduleId);
    }

    static bool ModuleAssemblyExists(string modDir, string assemblyName) =>
        ResolveSatelliteAssemblyPath(modDir, assemblyName) is not null;

    static Assembly? LoadAssembly(string modDir, string assemblyName, string moduleId) {
        var path = ResolveSatelliteAssemblyPath(modDir, assemblyName);
        if (path is null)
            return null;

        try {
            return ModAssemblyLoader.LoadFromModPath(path);
        }
        catch (ReflectionTypeLoadException ex) {
            var details = string.Join("; ", ex.LoaderExceptions?.Select(e => e?.Message) ?? []);
            LogModuleSkip(moduleId, $"failed to load {assemblyName} ({details}).");
            return null;
        }
    }

    static string? ResolveSatelliteAssemblyPath(string modDir, string assemblyName) =>
        KitLibHostPaths.TryResolveSatelliteAssemblyPath(modDir, assemblyName);

    static void AssociateSatelliteAssembly(Assembly assembly) {
        if (assembly == typeof(MainFile).Assembly)
            return;

        ModAssemblyAssociation.Associate(MainFile.ModID, assembly);
    }
}
