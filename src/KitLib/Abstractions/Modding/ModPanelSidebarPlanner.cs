using System;
using System.Collections.Generic;
using System.Linq;

namespace KitLib.Abstractions.Modding;

/// <summary>Pure sidebar list planning (registry → row count / initial selection).</summary>
public static class ModPanelSidebarPlanner {
    public static IReadOnlyList<KitLibModEntry> OrderForSidebar(IReadOnlyList<KitLibModEntry> snapshot) {
        if (snapshot.Count <= 1)
            return snapshot;
        var list = snapshot.ToList();
        list.Sort(static (a, b) =>
            string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static string ResolveShowcaseModId(
        IReadOnlyList<KitLibModEntry> snapshot,
        string? panelAssemblyName,
        Func<string?, bool> isRitsuFramework,
        Func<KitLibModEntry, bool> isSelectable) {
        if (!string.IsNullOrWhiteSpace(panelAssemblyName)
            && !isRitsuFramework(panelAssemblyName)) {
            foreach (var e in snapshot) {
                if (string.Equals(e.Id, panelAssemblyName, StringComparison.OrdinalIgnoreCase) && isSelectable(e))
                    return e.Id;
            }
        }
        foreach (var e in snapshot) {
            if (isRitsuFramework(e.Id))
                continue;
            if (isSelectable(e))
                return e.Id;
        }
        return string.IsNullOrWhiteSpace(panelAssemblyName) ? "KitLib" : panelAssemblyName;
    }

    public static ModPanelSidebarPlan Plan(
        IReadOnlyList<KitLibModEntry> snapshot,
        string? panelAssemblyName,
        Func<string?, bool> isRitsuFramework,
        Func<KitLibModEntry, bool> isSelectable) {
        var ordered = OrderForSidebar(snapshot);
        var initial = ResolveShowcaseModId(ordered, panelAssemblyName, isRitsuFramework, isSelectable);
        if (ordered.Count > 0) {
            var hasInitial = ordered.Any(e =>
                string.Equals(e.Id, initial, StringComparison.OrdinalIgnoreCase));
            if (!hasInitial)
                initial = ordered[0].Id;
        }
        return new ModPanelSidebarPlan(ordered, initial);
    }
}

public readonly record struct ModPanelSidebarPlan(
    IReadOnlyList<KitLibModEntry> OrderedMods,
    string InitialSelectedModId) {
    public int ExpectedRowCount => OrderedMods.Count;
}
