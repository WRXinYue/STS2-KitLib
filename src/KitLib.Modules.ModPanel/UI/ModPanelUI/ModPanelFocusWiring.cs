using System;
using System.Collections.Generic;
using Godot;

namespace KitLib.UI;

internal static class ModPanelFocusWiring {
    public static void Wire(IReadOnlyList<SidebarModRowVm> rows, string selectedModId, string selectedPageId,
        HBoxContainer pageTabRow, Control contentRoot, Control? scopeFocusTarget) {
        var selectedRow = FindRow(rows, selectedModId);
        var tabs = CollectPageTabs(pageTabRow);
        WirePageTabFocusNeighbors(tabs);
        var contentEntry = FindFirstFocusableDescendant(contentRoot);
        if (selectedRow == null)
            return;
        if (tabs.Count > 0) {
            var activeTab = tabs[FindSelectedTabIndex(tabs, selectedPageId)];
            tabs[0].FocusNeighborLeft = selectedRow.GetPath();
            foreach (var tab in tabs) {
                if (contentEntry != null)
                    tab.FocusNeighborBottom = contentEntry.GetPath();
            }
            if (contentEntry != null)
                contentEntry.FocusNeighborLeft = activeTab.GetPath();
        }
        else if (contentEntry != null) {
            contentEntry.FocusNeighborLeft = selectedRow.GetPath();
        }
        if (scopeFocusTarget != null && rows.Count > 0)
            scopeFocusTarget.FocusNeighborTop = rows[^1].Host.GetPath();
    }

    private static void WirePageTabFocusNeighbors(IReadOnlyList<Button> tabs) {
        for (var i = 0; i < tabs.Count; i++) {
            var tab = tabs[i];
            tab.FocusNeighborTop = tab.GetPath();
            tab.FocusNeighborBottom = tab.GetPath();
            if (i == 0)
                tab.FocusNeighborLeft = tab.GetPath();
            else
                tab.FocusNeighborLeft = tabs[i - 1].GetPath();
            if (i == tabs.Count - 1)
                tab.FocusNeighborRight = tab.GetPath();
            else
                tab.FocusNeighborRight = tabs[i + 1].GetPath();
        }
    }

    private static List<Button> CollectPageTabs(HBoxContainer pageTabRow) {
        var tabs = new List<Button>();
        foreach (var child in pageTabRow.GetChildren()) {
            if (child is Button b && b.Visible && b.FocusMode != Control.FocusModeEnum.None)
                tabs.Add(b);
        }
        return tabs;
    }

    private static Control? FindRow(IReadOnlyList<SidebarModRowVm> rows, string selectedModId) {
        foreach (var row in rows) {
            if (string.Equals(row.Id, selectedModId, StringComparison.OrdinalIgnoreCase))
                return row.Host;
        }
        return rows.Count > 0 ? rows[0].Host : null;
    }

    private static int FindSelectedTabIndex(IReadOnlyList<Button> tabs, string selectedPageId) {
        for (var i = 0; i < tabs.Count; i++) {
            if (tabs[i].HasMeta("pageId")
                && string.Equals(tabs[i].GetMeta("pageId").AsString(), selectedPageId, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    public static Control? FindFirstFocusableDescendant(Control root) {
        if (root.FocusMode != Control.FocusModeEnum.None && root.Visible)
            return root;
        foreach (var child in root.GetChildren()) {
            if (child is not Control c || !c.Visible)
                continue;
            var found = FindFirstFocusableDescendant(c);
            if (found != null)
                return found;
        }
        return null;
    }
}
