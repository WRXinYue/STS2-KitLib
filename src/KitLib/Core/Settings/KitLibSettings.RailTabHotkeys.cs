using System;
using System.Collections.Generic;

namespace KitLib.Settings;

public sealed partial class KitLibSettings {
    /// <summary>Per DevMode rail panel shortcut overrides (tab id → binding).</summary>
    public Dictionary<string, HotkeyBinding> RailTabHotkeys { get; set; } =
        new(StringComparer.Ordinal);

    internal HotkeyBinding GetRailTabHotkey(string tabId) {
        if (RailTabHotkeys.TryGetValue(tabId, out var saved) && saved.KeyCode != 0)
            return saved.Clone();
        return RailTabHotkeyDefaults.ForTab(tabId);
    }

    internal void SetRailTabHotkey(string tabId, HotkeyBinding binding) {
        RailTabHotkeys[tabId] = binding.Clone();
    }

    internal IEnumerable<(string TabId, HotkeyBinding Binding)> EnumerateRailTabHotkeys() {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tabId in RailTabHotkeyDefaults.ByTabId.Keys) {
            seen.Add(tabId);
            yield return (tabId, GetRailTabHotkey(tabId));
        }
        foreach (var (tabId, binding) in RailTabHotkeys) {
            if (seen.Add(tabId) && binding.KeyCode != 0)
                yield return (tabId, binding.Clone());
        }
    }
}
