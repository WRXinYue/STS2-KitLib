using System;

namespace KitLib.Settings;

internal static class RailTabHotkeyActionId {
    internal const string Prefix = "railTab:";

    internal static string ForTab(string tabId) => Prefix + tabId;

    internal static bool TryParseTabId(string actionId, out string tabId) {
        if (actionId.StartsWith(Prefix, StringComparison.Ordinal)) {
            tabId = actionId[Prefix.Length..];
            return tabId.Length > 0;
        }
        tabId = "";
        return false;
    }

    internal static bool IsRailTabAction(string actionId) =>
        actionId.StartsWith(Prefix, StringComparison.Ordinal);
}
