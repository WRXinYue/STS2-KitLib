using System.Reflection;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Host;

/// <summary>
/// Loads optional KitLib satellite DLLs from <c>mods/KitLib/modules/</c>.
/// Skips a module when it is missing, already initialized externally, has unmet
/// prerequisites, or fails to load (conflict / init error).
/// </summary>
internal static class SatelliteModuleLoader {
    internal const string ModulesSubdir = "modules";
    private static Assembly? _devAssembly;
    sealed record ModuleSpec(
        string ModuleId,
        string AssemblyName,
        string? EntryTypeName,
        string[] Requires);

    static readonly (string AssemblyName, string[] BeforeModuleIds)[] PreloadBeforeInit = [
        ("KitLib.Cheat", [ModuleIds.Ai, ModuleIds.Panel]),
        ("KitLib.Dev", [ModuleIds.Panel]),
    ];

    static readonly ModuleSpec[] LoadOrder = [
        new(ModuleIds.User, "KitLib.User", "KitLib.User.ModuleEntry", []),
        new(ModuleIds.Ai, "KitLib.AI", "KitLib.AI.ModuleEntry", []),
        new(ModuleIds.ModPanel, "KitLib.ModPanel", "KitLib.ModPanelMod.ModuleEntry", []),
        new(ModuleIds.Panel, "KitLib.Panel", "KitLib.PanelMod.ModuleEntry", []),
        new(ModuleIds.Cheat, "KitLib.Cheat", "KitLib.Cheat.ModuleEntry", [ModuleIds.Panel]),
        new(ModuleIds.Dev, "KitLib.Dev", "KitLib.Dev.ModuleEntry", [ModuleIds.Panel]),
    ];

    internal static void LoadBundledModules() {
        var modDir = Path.GetDirectoryName(typeof(MainFile).Assembly.Location);
        if (string.IsNullOrEmpty(modDir)) {
            MainFile.Logger.Warn("Satellite loader: cannot resolve mod directory.");
            return;
        }

        ModAssemblyLoader.EnsureResolveHook(modDir);
        MainFile.Logger.Info($"Satellite loader: modDir={modDir}");

        var loaded = new List<string>();
        foreach (var spec in LoadOrder) {
            MainFile.Logger.Info($"Satellite loader: trying {spec.ModuleId} ({spec.AssemblyName}.dll).");
            PreloadSatelliteDependencies(modDir, spec.ModuleId);
            if (TryLoadModule(modDir, spec))
                loaded.Add(spec.ModuleId);
        }

        if (ModuleCatalog.IsLoaded(ModuleIds.Dev)) {
            var devAssembly = _devAssembly ?? ModAssemblyLoader.GetLoadedAssembly("KitLib.Dev");
            WireDevModuleDelegates(devAssembly);
            ApplyDevHarmony(devAssembly);
        }

        if (loaded.Count == 0)
            MainFile.Logger.Info("Satellite loader done: no bundled modules loaded.");
        else
            MainFile.Logger.Info($"Satellite loader done: loaded {loaded.Count} — {string.Join(", ", loaded)}.");
    }

    static void PreloadSatelliteDependencies(string modDir, string moduleId) {
        foreach (var (assemblyName, beforeModuleIds) in PreloadBeforeInit) {
            if (!Array.Exists(beforeModuleIds, id => string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase)))
                continue;
            TryPreloadAssembly(modDir, assemblyName);
        }
    }

    static void TryPreloadAssembly(string modDir, string assemblyName) {
        var modulesDir = Path.Combine(modDir, ModulesSubdir);
        var path = Path.Combine(modulesDir, assemblyName + ".dll");
        if (!File.Exists(path)) {
            path = Path.Combine(modDir, assemblyName + ".dll");
            if (!File.Exists(path))
                return;
        }

        try {
            ModAssemblyLoader.LoadFromModPath(path);
        }
        catch (Exception ex) {
            KitLog.Warn($"Preload {assemblyName} failed — {ex.Message}");
        }
    }

    static bool TryLoadModule(string modDir, ModuleSpec spec) {
        if (!Sts2RuntimeProfile.AllowHighRiskModules
            && (spec.ModuleId == ModuleIds.Cheat || spec.ModuleId == ModuleIds.Dev)) {
            KitLog.Warn($"Module {spec.ModuleId} skipped — STS2 profile {Sts2RuntimeProfile.Current} is unsupported or sanity mismatch.");
            return false;
        }

        if (ModuleCatalog.IsLoaded(spec.ModuleId)) {
            KitLog.Info($"Module {spec.ModuleId} already active — skipping bundled load.");
            return true;
        }

        if (IsExternallyInstalled(spec.ModuleId)) {
            KitLog.Info($"Module {spec.ModuleId} installed as separate mod — skipping bundled load.");
            return ModuleCatalog.IsLoaded(spec.ModuleId);
        }

        foreach (var required in spec.Requires) {
            if (!ModuleCatalog.IsLoaded(required)) {
                KitLog.Warn($"Module {spec.ModuleId} skipped — prerequisite {required} is not loaded.");
                return false;
            }
        }

        try {
            KitLog.Info($"Satellite loader: loading {spec.AssemblyName}.dll assembly file.");
            var assembly = LoadAssembly(modDir, spec.AssemblyName, spec.ModuleId);
            if (assembly == null)
                return false;
            KitLog.Info($"Satellite loader: {spec.AssemblyName} assembly loaded.");

            if (spec.EntryTypeName == null) {
                ModuleCatalog.Announce(spec.ModuleId);
                KitLog.Info($"Loaded passive module {spec.ModuleId}.");
                return true;
            }

            KitLog.Info($"Satellite loader: resolving entry {spec.EntryTypeName}.");
            var entryType = assembly.GetType(spec.EntryTypeName, throwOnError: false);
            if (entryType == null) {
                KitLog.Warn($"Module {spec.ModuleId} skipped — entry type {spec.EntryTypeName} not found.");
                return false;
            }

            var init = entryType.GetMethod(
                "Initialize",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            if (init == null) {
                KitLog.Warn($"Module {spec.ModuleId} skipped — Initialize() not found.");
                return false;
            }

            InvokeModuleInitialize(spec.ModuleId, init);

            if (spec.ModuleId == ModuleIds.Dev) {
                _devAssembly = assembly;
            }

            if (!ModuleCatalog.IsLoaded(spec.ModuleId))
                ModuleCatalog.Announce(spec.ModuleId);
            return ModuleCatalog.IsLoaded(spec.ModuleId);
        }
        catch (TargetInvocationException ex) {
            KitLog.Warn($"Module {spec.ModuleId} init failed — skipped ({ex.InnerException?.Message ?? ex.Message}).");
            return false;
        }
        catch (Exception ex) {
            KitLog.Warn($"Module {spec.ModuleId} load conflict — skipped ({ex.Message}).");
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
            KitLog.Warn($"Module {moduleId} init failed — skipped ({ex.InnerException?.Message ?? ex.Message}).");
        }
        catch (Exception ex) {
            KitLog.Warn($"Module {moduleId} init failed — skipped ({ex.Message}).");
        }
    }

    static Assembly? LoadAssembly(string modDir, string assemblyName, string moduleId) {
        var modulesDir = Path.Combine(modDir, ModulesSubdir);
        var path = Path.Combine(modulesDir, assemblyName + ".dll");
        if (!File.Exists(path)) {
            var legacyPath = Path.Combine(modDir, assemblyName + ".dll");
            if (!File.Exists(legacyPath)) {
                KitLog.Info($"Module {moduleId} not present ({assemblyName}.dll).");
                return null;
            }

            path = legacyPath;
            KitLog.Info($"Loading {assemblyName} from mod root (legacy layout). Run make sync-full to move it under {ModulesSubdir}/.");
        }

        try {
            return ModAssemblyLoader.LoadFromModPath(path);
        }
        catch (ReflectionTypeLoadException ex) {
            var details = string.Join("; ", ex.LoaderExceptions?.Select(e => e?.Message) ?? []);
            KitLog.Warn($"Module {moduleId} skipped — failed to load {assemblyName} ({details}).");
            return null;
        }
    }

    static bool IsExternallyInstalled(string moduleId) {
        if (string.Equals(moduleId, ModuleIds.Core, StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var mod in EnumerateLoadedMods()) {
            var id = mod.manifest?.id;
            if (!string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase))
                continue;
            return true;
        }

        return false;
    }

    static IEnumerable<Mod> EnumerateLoadedMods() {
        var method = typeof(ModManager).GetMethod(
            "GetLoadedMods",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        if (method == null)
            return [];
        return (IEnumerable<Mod>)method.Invoke(null, null)!;
    }

}
