using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KitLib.Modding;
using Godot;
namespace KitLib.Integration;
/// <summary>
/// Optional STS2-RitsuLib <c>ModSettingsRegistry</c> via reflection (no compile-time reference to RitsuLib).
/// </summary>
public static class RitsuModSettingsBridge {
    /// <summary>Short manifest id for RitsuLib; <see cref="IsRitsuFrameworkModId"/> also treats <c>com.ritsukage.sts2-RitsuLib</c> as the same mod.</summary>
    public const string RitsuFrameworkModId = "STS2-RitsuLib";
    private const string RitsuAssemblyName = "STS2-RitsuLib";
    private const string RegistryFullName = "STS2RitsuLib.Settings.ModSettingsRegistry";
    private const string LocalizationFullName = "STS2RitsuLib.Settings.ModSettingsLocalization";
    private const string UiContextFullName = "STS2RitsuLib.Settings.ModSettingsUiContext";
    public static bool IsAvailable => TryGetRitsuAssembly() != null;
    public static bool IsRitsuFrameworkModId(string? modId) {
        if (string.IsNullOrWhiteSpace(modId))
            return false;
        if (string.Equals(modId, RitsuFrameworkModId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(modId, "RitsuLib", StringComparison.OrdinalIgnoreCase))
            return true;
        // Ritsu may register pages under reverse-DNS while manifest id stays STS2-RitsuLib.
        return string.Equals(modId, "com.ritsukage.sts2-RitsuLib", StringComparison.OrdinalIgnoreCase);
    }
    public static string GetPageId(object page) => GetStringProperty(page, "Id");
    public static string GetPageModId(object page) => GetStringProperty(page, "ModId");
    /// <summary>True when <paramref name="pageModId"/> belongs to the same mod as sidebar <paramref name="selectedModId"/>.</summary>
    private static bool SettingsPageModIdMatches(string pageModId, string selectedModId) {
        if (string.IsNullOrWhiteSpace(pageModId))
            return false;
        if (string.Equals(pageModId, selectedModId, StringComparison.OrdinalIgnoreCase))
            return true;
        return IsRitsuFrameworkModId(pageModId) && IsRitsuFrameworkModId(selectedModId);
    }
    public static Assembly? TryGetRitsuAssembly() {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
            if (string.Equals(a.GetName().Name, RitsuAssemblyName, StringComparison.OrdinalIgnoreCase))
                return a;
        }
        return null;
    }
    /// <summary>Distinct mod ids that have at least one registered settings page (any depth).</summary>
    public static IReadOnlyList<string> GetModIdsWithRegisteredPages() {
        var pages = EnumerateRegisteredPageObjects();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in pages) {
            var id = GetStringProperty(p, "ModId");
            if (!string.IsNullOrWhiteSpace(id))
                set.Add(id);
        }
        return set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static string TryResolveModDisplayName(string modId) {
        if (string.IsNullOrWhiteSpace(modId))
            return modId;
        var asm = TryGetRitsuAssembly();
        if (asm == null)
            return modId;
        var loc = asm.GetType(LocalizationFullName);
        if (loc == null)
            return modId;
        var m = loc.GetMethod("ResolveModName", BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(string), typeof(string) }, null);
        if (m == null)
            return modId;
        try {
            var s = m.Invoke(null, new object[] { modId, modId }) as string;
            return string.IsNullOrWhiteSpace(s) ? modId : s;
        }
        catch {
            return modId;
        }
    }
    /// <summary>Root-level <c>ModSettingsPage</c> instances for <paramref name="modId"/> (RitsuLib registry).</summary>
    public static IReadOnlyList<object> GetRootPageObjects(string modId) {
        if (string.IsNullOrWhiteSpace(modId))
            return Array.Empty<object>();
        var list = new List<object>();
        foreach (var page in EnumerateRegisteredPageObjects()) {
            if (!SettingsPageModIdMatches(GetStringProperty(page, "ModId"), modId))
                continue;
            if (!string.IsNullOrWhiteSpace(GetStringProperty(page, "ParentPageId")))
                continue;
            list.Add(page);
        }
        list.Sort(static (a, b) =>
            string.Compare(GetStringProperty(a, "Id"), GetStringProperty(b, "Id"), StringComparison.OrdinalIgnoreCase));
        return list;
    }
    /// <summary>
    /// All <c>ModSettingsPage</c> rows for <paramref name="modId"/> (roots and children), depth-first like RitsuLib’s sidebar.
    /// DevMode uses this so nested pages (e.g. under “General”) are reachable, not only root tabs.
    /// </summary>
    public static IReadOnlyList<object> GetAllPageObjects(string modId) {
        if (string.IsNullOrWhiteSpace(modId))
            return Array.Empty<object>();
        var asm = TryGetRitsuAssembly();
        if (asm == null)
            return Array.Empty<object>();
        var regType = asm.GetType(RegistryFullName);
        var list = new List<object>();
        foreach (var page in EnumerateRegisteredPageObjects()) {
            if (!SettingsPageModIdMatches(GetStringProperty(page, "ModId"), modId))
                continue;
            list.Add(page);
        }
        if (list.Count == 0)
            return list;
        MethodInfo? sortMethod = null;
        if (regType != null)
            sortMethod = regType.GetMethod("GetEffectivePageSortOrder", BindingFlags.Public | BindingFlags.Static,
                null, new[] { list[0].GetType() }, null);
        void SortPages(List<object> pages) {
            if (sortMethod != null) {
                pages.Sort((a, b) => {
                    var oa = 0;
                    var ob = 0;
                    try {
                        oa = (int)(sortMethod.Invoke(null, new[] { a }) ?? 0);
                        ob = (int)(sortMethod.Invoke(null, new[] { b }) ?? 0);
                    }
                    catch {
                        // ignored
                    }
                    var c = oa.CompareTo(ob);
                    return c != 0
                        ? c
                        : string.Compare(GetPageId(a), GetPageId(b), StringComparison.OrdinalIgnoreCase);
                });
            }
            else {
                pages.Sort(static (a, b) =>
                    string.Compare(GetPageId(a), GetPageId(b), StringComparison.OrdinalIgnoreCase));
            }
        }
        var roots = list.Where(p => string.IsNullOrWhiteSpace(GetStringProperty(p, "ParentPageId"))).ToList();
        SortPages(roots);
        var ordered = new List<object>();
        var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(object page) {
            var id = GetPageId(page);
            if (string.IsNullOrWhiteSpace(id) || !included.Add(id))
                return;
            ordered.Add(page);
            var children = list
                .Where(p => string.Equals(GetStringProperty(p, "ParentPageId"), id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            SortPages(children);
            foreach (var c in children)
                Visit(c);
        }
        foreach (var r in roots)
            Visit(r);
        foreach (var p in list) {
            var id = GetPageId(p);
            if (!string.IsNullOrWhiteSpace(id) && included.Add(id))
                ordered.Add(p);
        }
        return ordered;
    }
    /// <summary>Short label for a page tab (breadcrumb when <c>ParentPageId</c> is set).</summary>
    public static string GetPageTabLabel(object page, string modId) {
        var parts = new List<string>();
        object? walk = page;
        var depth = 0;
        while (walk != null && depth++ < 24) {
            parts.Add(ResolvePageTitle(walk));
            var parentId = GetStringProperty(walk, "ParentPageId");
            if (string.IsNullOrWhiteSpace(parentId))
                break;
            walk = TryFindPageObject(modId, parentId);
        }
        parts.Reverse();
        return parts.Count > 0 ? string.Join(" › ", parts) : ResolvePageTitle(page);
    }
    private static object? TryFindPageObject(string modId, string pageId) {
        foreach (var page in EnumerateRegisteredPageObjects()) {
            if (!SettingsPageModIdMatches(GetStringProperty(page, "ModId"), modId))
                continue;
            if (string.Equals(GetPageId(page), pageId, StringComparison.OrdinalIgnoreCase))
                return page;
        }
        return null;
    }
    /// <summary>Builds the interactive Ritsu settings body (toggles, lists, etc.) for one page.</summary>
    public static Control? TryCreateInteractivePageBody(Node ritsuSubmenu, string modId, object modSettingsPage,
        out string? error) {
        error = null;
        if (ritsuSubmenu == null)
            return null;
        var asm = TryGetRitsuAssembly();
        if (asm == null) {
            error = "Ritsu assembly missing";
            return null;
        }
        var contextType = asm.GetType(UiContextFullName);
        if (contextType == null) {
            error = "Ritsu settings UI types missing";
            return null;
        }
        if (RitsuModSettingsDevPageBodyBuilder.TryBuild(ritsuSubmenu, modId, modSettingsPage, asm, out var built,
                out var buildErr))
            return built;
        error = buildErr ?? "KitLib page body build failed";
        return null;
    }
    /// <summary>Root pages for <paramref name="modId"/> with section summaries for DevMode read-only UI.</summary>
    public static IReadOnlyList<RitsuRootPageSummary> GetRootPageSummaries(string modId) {
        if (string.IsNullOrWhiteSpace(modId))
            return Array.Empty<RitsuRootPageSummary>();
        var result = new List<RitsuRootPageSummary>();
        foreach (var page in EnumerateRegisteredPageObjects()) {
            if (!SettingsPageModIdMatches(GetStringProperty(page, "ModId"), modId))
                continue;
            var parent = GetStringProperty(page, "ParentPageId");
            if (!string.IsNullOrWhiteSpace(parent))
                continue;
            var pageId = GetStringProperty(page, "Id");
            var title = ResolvePageTitle(page);
            var sections = SummarizeSections(page);
            result.Add(new RitsuRootPageSummary(pageId ?? "", title, sections));
        }
        return result
            .OrderBy(p => p.PageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    private static IEnumerable<object> EnumerateRegisteredPageObjects() {
        var asm = TryGetRitsuAssembly();
        if (asm == null)
            yield break;
        var reg = asm.GetType(RegistryFullName);
        if (reg == null)
            yield break;
        var getPages = reg.GetMethod("GetPages", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
        if (getPages == null)
            yield break;
        object? pagesObj;
        try {
            pagesObj = getPages.Invoke(null, null);
        }
        catch {
            yield break;
        }
        if (pagesObj is not IEnumerable enumerable)
            yield break;
        foreach (var item in enumerable) {
            if (item != null)
                yield return item;
        }
    }
    private static string GetStringProperty(object target, string name) {
        var v = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
        return v as string ?? "";
    }
    private static string ResolvePageTitle(object page) {
        var pageId = GetStringProperty(page, "Id");
        var titleObj = page.GetType().GetProperty("Title", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(page);
        if (titleObj == null)
            return string.IsNullOrWhiteSpace(pageId) ? "—" : pageId;
        var resolve = titleObj.GetType().GetMethod("Resolve", BindingFlags.Public | BindingFlags.Instance,
            Type.EmptyTypes);
        if (resolve == null)
            return string.IsNullOrWhiteSpace(pageId) ? "—" : pageId;
        try {
            var s = resolve.Invoke(titleObj, null) as string;
            return string.IsNullOrWhiteSpace(s) ? pageId : s;
        }
        catch {
            return pageId;
        }
    }
    private static IReadOnlyList<RitsuSectionSummary> SummarizeSections(object page) {
        var sectionsProp = page.GetType().GetProperty("Sections", BindingFlags.Public | BindingFlags.Instance);
        var sectionsObj = sectionsProp?.GetValue(page);
        if (sectionsObj is not IEnumerable enumerable)
            return Array.Empty<RitsuSectionSummary>();
        var list = new List<RitsuSectionSummary>();
        foreach (var sec in enumerable) {
            if (sec == null)
                continue;
            var sid = GetStringProperty(sec, "Id");
            var stitle = ResolveSectionTitle(sec);
            var entryCount = CountEntries(sec);
            list.Add(new RitsuSectionSummary(sid, stitle, entryCount));
        }
        return list;
    }
    private static string ResolveSectionTitle(object section) {
        var sid = GetStringProperty(section, "Id");
        var titleObj = section.GetType().GetProperty("Title", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(section);
        if (titleObj == null)
            return string.IsNullOrWhiteSpace(sid) ? "—" : sid;
        var resolve = titleObj.GetType().GetMethod("Resolve", BindingFlags.Public | BindingFlags.Instance,
            Type.EmptyTypes);
        if (resolve == null)
            return sid;
        try {
            var s = resolve.Invoke(titleObj, null) as string;
            return string.IsNullOrWhiteSpace(s) ? sid : s;
        }
        catch {
            return sid;
        }
    }
    private static int CountEntries(object section) {
        var entries = section.GetType().GetProperty("Entries", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(section);
        if (entries is ICollection c)
            return c.Count;
        if (entries is IEnumerable e) {
            var n = 0;
            foreach (var _ in e)
                n++;
            return n;
        }
        return 0;
    }
}
public readonly record struct RitsuRootPageSummary(string PageId, string Title, IReadOnlyList<RitsuSectionSummary> Sections);
public readonly record struct RitsuSectionSummary(string SectionId, string Title, int EntryCount);