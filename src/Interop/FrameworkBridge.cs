using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace KitLib.Interop;

/// <summary>
/// Read-only RitsuLib diagnostics for DevMode (no config sync).
/// Binds to RitsuLib via reflection so DevMode works without RitsuLib installed.
/// </summary>
public static class FrameworkBridge {
    private const int MaxPageInventoryLines = 48;
    private const string RitsuFrameworkTypeName = "STS2RitsuLib.RitsuLibFramework";

    private static IDisposable? _ritsuLifecycleSub;

    // Lazily resolved RitsuLib type (null when not installed)
    private static Type? _frameworkType;
    private static bool _typeResolved;

    private static Type? FrameworkType {
        get {
            if (_typeResolved) return _frameworkType;
            _typeResolved = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
                _frameworkType = asm.GetType(RitsuFrameworkTypeName, throwOnError: false);
                if (_frameworkType != null) break;
            }
            return _frameworkType;
        }
    }

    public static bool IsAvailable => FrameworkType != null;

    public readonly record struct FrameworkBridgeSnapshot(
        // RitsuLib — manifest / identity
        string RitsuDisplayName,
        string RitsuManifestVersion,
        string RitsuLibFrameworkModId,
        string RitsuLibAssemblyVersion,
        string RitsuSettingsRootKey,
        string RitsuSettingsFileName,
        // RitsuLib — runtime
        bool RitsuLibInitialized,
        bool RitsuLibActive,
        bool RitsuLibHasModSettingsPages,
        int RitsuLibModSettingsPageCount,
        int RitsuLibDistinctOwningModCount,
        int RitsuLibTotalSectionCount,
        string RitsuLibPagesInventoryLines,
        // Harmony (process-wide)
        HarmonyPatchSummary.Stats HarmonyStats);

    /// <summary>Subscribe once to RitsuLib lifecycle (replayable) and log a one-line snapshot.</summary>
    public static void Initialize() {
        if (!IsAvailable) {
            MainFile.Logger.Info("[KitLib Bridge] RitsuLib not present — bridge disabled.");
            return;
        }

        try {
            // RitsuLibFramework.SubscribeLifecycle<FrameworkInitializedEvent>(handler)
            var subscribeMethod = FrameworkType!
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "SubscribeLifecycle" && m.IsGenericMethod);

            if (subscribeMethod != null) {
                var eventType = FrameworkType.Assembly.GetType("STS2RitsuLib.FrameworkInitializedEvent");
                if (eventType != null) {
                    var generic = subscribeMethod.MakeGenericMethod(eventType);
                    var delegateType = typeof(Action<>).MakeGenericType(eventType);
                    var handler = CreateFrameworkEventHandler(eventType, delegateType);
                    if (handler != null)
                        _ritsuLifecycleSub = generic.Invoke(null, [handler]) as IDisposable;
                }
            }
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib Bridge] RitsuLib lifecycle subscribe failed: {ex.Message}");
        }

        try {
            var s = CaptureSnapshot();
            MainFile.Logger.Info(
                $"[KitLib Bridge] Ritsu init={s.RitsuLibInitialized} active={s.RitsuLibActive} " +
                $"pages={s.RitsuLibModSettingsPageCount} mods={s.RitsuLibDistinctOwningModCount} | " +
                $"harmony methods={s.HarmonyStats.PatchedMethodCount}");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib Bridge] snapshot failed: {ex.Message}");
        }
    }

    public static FrameworkBridgeSnapshot CaptureSnapshot() {
        var harmony = HarmonyPatchSummary.Aggregate();

        if (!IsAvailable) {
            return new FrameworkBridgeSnapshot(
                "—", "—", "—", "—", "—", "—",
                false, false, false, 0, 0, 0, "—",
                harmony);
        }

        var ft = FrameworkType!;
        bool ritsuInit   = GetStaticBool(ft, "IsInitialized");
        bool ritsuActive = GetStaticBool(ft, "IsActive");
        bool ritsuHasPages = GetStaticBool(ft, "HasRegisteredModSettings");
        string ritsuVer  = ft.Assembly.GetName().Version?.ToString() ?? "?";

        var pages = GetModSettingsPages(ft);
        int pageCount    = pages.Count;
        int distinctMods = pages.Select(p => p.modId).Distinct().Count();
        int totalSections = pages.Sum(p => p.sectionCount);
        string inventory = BuildPageInventory(pages);

        var constType = ft.Assembly.GetType("STS2RitsuLib.Const");
        string ritsuName         = GetConstString(constType, "Name")        ?? "RitsuLib";
        string ritsuVersion      = GetConstString(constType, "Version")     ?? "?";
        string ritsuModId        = GetConstString(constType, "ModId")       ?? "?";
        string ritsuSettingsKey  = GetConstString(constType, "SettingsKey") ?? "?";
        string ritsuSettingsFile = GetConstString(constType, "SettingsFileName") ?? "?";

        return new FrameworkBridgeSnapshot(
            ritsuName,
            ritsuVersion,
            ritsuModId,
            ritsuVer,
            ritsuSettingsKey,
            ritsuSettingsFile,
            ritsuInit,
            ritsuActive,
            ritsuHasPages,
            pageCount,
            distinctMods,
            totalSections,
            inventory,
            harmony);
    }

    // ──────── Reflection helpers ────────

    private static string? GetConstString(Type? type, string fieldOrPropertyName) {
        if (type == null) return null;
        try {
            return type.GetField(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Static)
                       ?.GetValue(null) as string
                ?? type.GetProperty(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Static)
                       ?.GetValue(null) as string;
        }
        catch { return null; }
    }

    private static bool GetStaticBool(Type type, string propertyName) {
        try {
            return (bool)(type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) ?? false);
        }
        catch { return false; }
    }

    private record PageEntry(string modId, string pageId, int sectionCount, int sortOrder,
        string parentPageId, string title);

    private static List<PageEntry> GetModSettingsPages(Type ft) {
        try {
            var getMethod = ft.GetMethod("GetRegisteredModSettings",
                BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (getMethod == null) return [];

            var result = getMethod.Invoke(null, null);
            if (result is not System.Collections.IEnumerable enumerable) return [];

            var list = new List<PageEntry>();
            foreach (var page in enumerable) {
                if (page == null) continue;
                var pageType = page.GetType();
                string modId      = pageType.GetProperty("ModId")?.GetValue(page) as string ?? "";
                string pageId     = pageType.GetProperty("Id")?.GetValue(page) as string ?? "";
                int sortOrder     = (int)(pageType.GetProperty("SortOrder")?.GetValue(page) ?? 0);
                string parentId   = pageType.GetProperty("ParentPageId")?.GetValue(page) as string ?? "";
                var sections      = pageType.GetProperty("Sections")?.GetValue(page) as System.Collections.ICollection;
                int sectionCount  = sections?.Count ?? 0;

                string title = "";
                try {
                    var titleProp = pageType.GetProperty("Title");
                    var titleVal  = titleProp?.GetValue(page);
                    if (titleVal != null) {
                        var resolveMethod = titleVal.GetType().GetMethod("Resolve", Type.EmptyTypes)
                            ?? titleVal.GetType().GetMethod("GetFormattedText", Type.EmptyTypes);
                        title = resolveMethod?.Invoke(titleVal, null) as string ?? titleVal.ToString() ?? "";
                    }
                }
                catch { }

                list.Add(new PageEntry(modId, pageId, sectionCount, sortOrder, parentId, title));
            }

            return list.OrderBy(p => p.modId).ThenBy(p => p.sortOrder).ThenBy(p => p.pageId).ToList();
        }
        catch { return []; }
    }

    private static string BuildPageInventory(List<PageEntry> pages) {
        if (pages.Count == 0) return "—";

        var sb = new StringBuilder();
        var n = Math.Min(pages.Count, MaxPageInventoryLines);
        for (var i = 0; i < n; i++) {
            var p = pages[i];
            var parent = string.IsNullOrEmpty(p.parentPageId) ? "—" : p.parentPageId;
            string title = p.title.Length > 42 ? p.title[..39] + "…" : p.title;
            sb.Append(p.modId).Append(" | ").Append(p.pageId).Append(" | ")
              .Append(p.sectionCount).Append(" | ").Append(p.sortOrder).Append(" | ").Append(parent);
            if (!string.IsNullOrEmpty(title))
                sb.Append(" | ").Append(title.Replace('\n', ' ').Replace('\r', ' '));
            sb.Append('\n');
        }
        if (pages.Count > MaxPageInventoryLines)
            sb.Append($"(+{pages.Count - MaxPageInventoryLines} more)");

        return sb.ToString().TrimEnd();
    }

    private static Delegate? CreateFrameworkEventHandler(Type eventType, Type delegateType) {
        try {
            // Build: (evt) => Logger.Info(...)
            var logMethod = typeof(FrameworkBridge).GetMethod(
                nameof(LogFrameworkEvent), BindingFlags.Static | BindingFlags.NonPublic)!;
            var typedLog = logMethod.MakeGenericMethod(eventType);
            return Delegate.CreateDelegate(delegateType, typedLog);
        }
        catch { return null; }
    }

    private static void LogFrameworkEvent<T>(T evt) {
        try {
            var type = typeof(T);
            string modId   = type.GetProperty("FrameworkModId")?.GetValue(evt) as string ?? "?";
            bool isActive  = (bool)(type.GetProperty("IsActive")?.GetValue(evt) ?? false);
            MainFile.Logger.Info(
                $"[KitLib Bridge] RitsuLib event: modId={modId}, active={isActive}");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib Bridge] event log failed: {ex.Message}");
        }
    }
}
