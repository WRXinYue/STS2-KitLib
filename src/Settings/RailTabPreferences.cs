using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Icons;
using KitLib.Host;
using KitLib.Panels;

namespace KitLib.Settings;

/// <summary>
/// Persists and resolves dev-rail tab order and visibility from <see cref="KitLibSettings"/>.
/// </summary>
internal static class RailTabPreferences {
    public const string PrimaryKey = "Primary";
    public const string UtilityKey = "Utility";
    public const string SettingsTabId = "devmode.settings";
    public const string AiHostTabId = "devmode.ai";
    public const string LogsTabId = "devmode.logs";

    public const string HarmonyAnalysisTabId = "devmode.harmonyAnalysis";
    public const string ScriptsTabId = "devmode.scripts";
    public const string FrameworksTabId = "devmode.frameworks";

    public static readonly string[] DefaultHiddenTabIds = KitLibSettings.DefaultHiddenRailTabIds;

    private static string GroupKey(DevPanelTabGroup group) =>
        group == DevPanelTabGroup.Primary ? PrimaryKey : UtilityKey;

    /// <summary>Tabs shown on the in-game rail (mode filter + user visibility + user order).</summary>
    public static IReadOnlyList<IDevPanelTab> GetRailTabs(DevPanelTabGroup group) {
        var byId = DevPanelRegistry.GetTabs(group).ToDictionary(t => t.Id);
        var orderedIds = ResolveOrder(group, byId.Keys);
        var hidden = SettingsStore.Current.RailHiddenTabIds;

        var result = new List<IDevPanelTab>();
        foreach (var id in orderedIds) {
            if (!byId.TryGetValue(id, out var tab))
                continue;
            if (!IsAvailableInCurrentMode(tab))
                continue;
            if (hidden.Contains(id) && id != SettingsTabId)
                continue;
            if (KitLibState.DualInstanceMinimalRail && id is not AiHostTabId and not LogsTabId)
                continue;
            result.Add(tab);
        }
        return result;
    }

    /// <summary>All tabs in a group for the settings editor (includes hidden / mode-locked entries).</summary>
    public static IReadOnlyList<RailTabEditorEntry> GetEditorEntries(DevPanelTabGroup group) {
        var byId = DevPanelRegistry.GetTabs(group).ToDictionary(t => t.Id);
        var orderedIds = ResolveOrder(group, byId.Keys);
        var hidden = SettingsStore.Current.RailHiddenTabIds;

        return orderedIds
            .Where(id => byId.ContainsKey(id))
            .Select(id => {
                var tab = byId[id];
                return new RailTabEditorEntry(
                    tab.Id,
                    tab.Icon,
                    tab.DisplayName,
                    !hidden.Contains(id) || id == SettingsTabId,
                    id == SettingsTabId,
                    !IsAvailableInCurrentMode(tab));
            })
            .ToList();
    }

    public static void SetOrder(DevPanelTabGroup group, IReadOnlyList<string> orderedIds) {
        SettingsStore.Current.RailTabOrder[GroupKey(group)] = orderedIds.ToList();
        SettingsStore.Save();
    }

    public static void SetVisible(string tabId, bool visible) {
        var hidden = SettingsStore.Current.RailHiddenTabIds;
        if (visible)
            hidden.Remove(tabId);
        else if (tabId != SettingsTabId)
            hidden.Add(tabId);
        SettingsStore.Save();
    }

    public static void ApplyDefaultHiddenTabs(KitLibSettings settings) {
        foreach (var id in DefaultHiddenTabIds)
            settings.RailHiddenTabIds.Add(id);
    }

    public static void ResetGroup(DevPanelTabGroup group) {
        SettingsStore.Current.RailTabOrder.Remove(GroupKey(group));
        var keys = DevPanelRegistry.GetTabs(group).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        SettingsStore.Current.RailHiddenTabIds.RemoveWhere(id => keys.Contains(id));
        if (group == DevPanelTabGroup.Primary) {
            foreach (var id in DefaultHiddenTabIds) {
                if (keys.Contains(id))
                    SettingsStore.Current.RailHiddenTabIds.Add(id);
            }
        }
        SettingsStore.Save();
    }

    public static bool CanHide(string tabId, DevPanelTabGroup group) {
        if (tabId == SettingsTabId)
            return false;
        if (SettingsStore.Current.RailHiddenTabIds.Contains(tabId))
            return true;
        int visible = GetRailTabs(group).Count;
        return visible > 1;
    }

    private static List<string> ResolveOrder(DevPanelTabGroup group, IEnumerable<string> knownIds) {
        var known = knownIds.ToHashSet(StringComparer.Ordinal);
        var saved = SettingsStore.Current.RailTabOrder.TryGetValue(GroupKey(group), out var list)
            ? list
            : null;

        var result = new List<string>();
        if (saved != null) {
            foreach (var id in saved) {
                if (known.Remove(id))
                    result.Add(id);
            }
        }

        foreach (var tab in DevPanelRegistry.GetTabs(group)) {
            if (known.Remove(tab.Id))
                result.Add(tab.Id);
        }

        return result;
    }

    private static bool IsAvailableInCurrentMode(IDevPanelTab tab) {
        if (KitLibState.CheatsInRun || KitLibCheatOps.CanUseMultiplayerCheats?.Invoke() == true) return true;
        // AI Host is Cheat-kind but must stay visible in DevPanel / LAN minimal-rail sessions.
        if (tab.Id is AiHostTabId or LogsTabId) return true;
        return tab.Kind == DevPanelTabKind.Developer;
    }
}

internal readonly record struct RailTabEditorEntry(
    string Id,
    MdiIcon Icon,
    string Label,
    bool Visible,
    bool PinVisible,
    bool ModeLocked);
