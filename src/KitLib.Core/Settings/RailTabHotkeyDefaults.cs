using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace KitLib.Settings;

/// <summary>Default Ctrl+Shift+mnemonic bindings for DevMode rail panels.</summary>
internal static class RailTabHotkeyDefaults {
    private static HotkeyBinding Tab(Key key) => HotkeyBinding.Of(key, ctrl: true, shift: true);

    internal static readonly IReadOnlyDictionary<string, HotkeyBinding> ByTabId =
        new Dictionary<string, HotkeyBinding>(StringComparer.Ordinal) {
            ["devmode.cards"] = Tab(Key.C),
            ["devmode.relics"] = Tab(Key.I),
            ["devmode.enemies"] = Tab(Key.K),
            ["devmode.powers"] = Tab(Key.W),
            ["devmode.potions"] = Tab(Key.F),
            ["devmode.events"] = Tab(Key.V),
            ["devmode.rooms"] = Tab(Key.M),
            ["devmode.console"] = Tab(Key.J),
            ["devmode.cheats"] = Tab(Key.H),
            ["devmode.presets"] = Tab(Key.B),
            ["devmode.cardtest"] = Tab(Key.Y),
            ["devmode.save"] = Tab(Key.Q),
            ["devmode.ai"] = Tab(Key.A),
            ["devmode.enemyIntent"] = Tab(Key.N),
            ["devmode.combatStats"] = Tab(Key.U),
            ["devmode.hooks"] = Tab(Key.G),
            ["devmode.settings"] = Tab(Key.Comma),
            ["devmode.logs"] = Tab(Key.Z),
        };

    internal static HotkeyBinding ForTab(string tabId) =>
        ByTabId.TryGetValue(tabId, out var binding) ? binding.Clone() : new HotkeyBinding();

    internal static void ApplyMissingTo(KitLibSettings settings) {
        settings.RailTabHotkeys ??= new Dictionary<string, HotkeyBinding>(StringComparer.Ordinal);
        foreach (var (tabId, defaultBinding) in ByTabId) {
            if (!settings.RailTabHotkeys.TryGetValue(tabId, out var saved) || saved.KeyCode == 0)
                settings.RailTabHotkeys[tabId] = defaultBinding.Clone();
        }
    }

    internal static void ResetAll(KitLibSettings settings) {
        settings.RailTabHotkeys = ByTabId.ToDictionary(
            static kv => kv.Key,
            static kv => kv.Value.Clone(),
            StringComparer.Ordinal);
    }
}
