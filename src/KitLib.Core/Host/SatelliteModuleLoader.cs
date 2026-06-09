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
    sealed record ModuleSpec(
        string ModuleId,
        string AssemblyName,
        string? EntryTypeName,
        string[] Requires);

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
            if (TryLoadModule(modDir, spec))
                loaded.Add(spec.ModuleId);
        }

        if (loaded.Count == 0)
            MainFile.Logger.Info("Satellite loader done: no bundled modules loaded.");
        else
            MainFile.Logger.Info($"Satellite loader done: loaded {loaded.Count} — {string.Join(", ", loaded)}.");
    }

    static bool TryLoadModule(string modDir, ModuleSpec spec) {
        if (ModuleCatalog.IsLoaded(spec.ModuleId)) {
            MainFile.Logger.Info($"[KitLib] Module {spec.ModuleId} already active — skipping bundled load.");
            return true;
        }

        if (IsExternallyInstalled(spec.ModuleId)) {
            MainFile.Logger.Info($"[KitLib] Module {spec.ModuleId} installed as separate mod — skipping bundled load.");
            return ModuleCatalog.IsLoaded(spec.ModuleId);
        }

        foreach (var required in spec.Requires) {
            if (!ModuleCatalog.IsLoaded(required)) {
                MainFile.Logger.Warn(
                    $"[KitLib] Module {spec.ModuleId} skipped — prerequisite {required} is not loaded.");
                return false;
            }
        }

        try {
            var assembly = LoadAssembly(modDir, spec.AssemblyName, spec.ModuleId);
            if (assembly == null)
                return false;

            if (spec.EntryTypeName == null) {
                ModuleCatalog.Announce(spec.ModuleId);
                MainFile.Logger.Info($"[KitLib] Loaded passive module {spec.ModuleId}.");
                return true;
            }

            var entryType = assembly.GetType(spec.EntryTypeName, throwOnError: false);
            if (entryType == null) {
                MainFile.Logger.Warn($"[KitLib] Module {spec.ModuleId} skipped — entry type {spec.EntryTypeName} not found.");
                return false;
            }

            var init = entryType.GetMethod(
                "Initialize",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            if (init == null) {
                MainFile.Logger.Warn($"[KitLib] Module {spec.ModuleId} skipped — Initialize() not found.");
                return false;
            }

            init.Invoke(null, null);
            if (!ModuleCatalog.IsLoaded(spec.ModuleId))
                ModuleCatalog.Announce(spec.ModuleId);
            return ModuleCatalog.IsLoaded(spec.ModuleId);
        }
        catch (TargetInvocationException ex) {
            MainFile.Logger.Warn(
                $"[KitLib] Module {spec.ModuleId} init failed — skipped ({ex.InnerException?.Message ?? ex.Message}).");
            return false;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib] Module {spec.ModuleId} load conflict — skipped ({ex.Message}).");
            return false;
        }
    }

    static Assembly? LoadAssembly(string modDir, string assemblyName, string moduleId) {
        var modulesDir = Path.Combine(modDir, ModulesSubdir);
        var path = Path.Combine(modulesDir, assemblyName + ".dll");
        if (!File.Exists(path)) {
            var legacyPath = Path.Combine(modDir, assemblyName + ".dll");
            if (!File.Exists(legacyPath)) {
                MainFile.Logger.Info($"[KitLib] Module {moduleId} not present ({assemblyName}.dll).");
                return null;
            }

            path = legacyPath;
            MainFile.Logger.Info(
                $"[KitLib] Loading {assemblyName} from mod root (legacy layout). Run make sync-full to move it under {ModulesSubdir}/.");
        }

        try {
            return ModAssemblyLoader.LoadFromModPath(path);
        }
        catch (ReflectionTypeLoadException ex) {
            var details = string.Join("; ", ex.LoaderExceptions?.Select(e => e?.Message) ?? []);
            MainFile.Logger.Warn(
                $"[KitLib] Module {moduleId} skipped — failed to load {assemblyName} ({details}).");
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
