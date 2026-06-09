using System;
using System.Collections.Generic;
using System.Linq;

namespace KitLib.Abstractions.Modding;

/// <summary>Pure sidebar list planning (catalog → row count / initial selection).</summary>
public static class ModPanelSidebarPlanner {
    public static IReadOnlyList<KitLibModInfo> OrderForSidebar(IReadOnlyList<KitLibModInfo> snapshot) {
        if (snapshot.Count <= 1)
            return snapshot;
        var list = snapshot.ToList();
        list.Sort(static (a, b) =>
            string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static string ResolveShowcaseModId(
        IReadOnlyList<KitLibModInfo> snapshot,
        string? panelAssemblyName,
        Func<string?, bool> isRitsuFramework,
        Func<string, bool> modExistsInGame) {
        if (!string.IsNullOrWhiteSpace(panelAssemblyName)
            && !isRitsuFramework(panelAssemblyName)
            && modExistsInGame(panelAssemblyName))
            return panelAssemblyName;
        foreach (var e in snapshot) {
            if (isRitsuFramework(e.Id))
                continue;
            if (modExistsInGame(e.Id))
                return e.Id;
        }
        return string.IsNullOrWhiteSpace(panelAssemblyName) ? "KitLib" : panelAssemblyName;
    }

    public static ModPanelSidebarPlan Plan(
        IReadOnlyList<KitLibModInfo> snapshot,
        string? panelAssemblyName,
        Func<string?, bool> isRitsuFramework,
        Func<string, bool> modExistsInGame) {
        var ordered = OrderForSidebar(snapshot);
        var initial = ResolveShowcaseModId(ordered, panelAssemblyName, isRitsuFramework, modExistsInGame);
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
    IReadOnlyList<KitLibModInfo> OrderedMods,
    string InitialSelectedModId) {
    public int ExpectedRowCount => OrderedMods.Count;
}
