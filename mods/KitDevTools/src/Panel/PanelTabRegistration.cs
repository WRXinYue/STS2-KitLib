using System;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Host;

namespace KitLib.PanelMod;

internal static class PanelTabRegistration {
    internal static void RegisterAutoSlay() {
        RegisterTab(
            "devmode.autoslay",
            "speedometer-medium",
            "panel.autoslay",
            "AutoSlay",
            860,
            KitLibTabGroup.Primary,
            KitLibTabKind.Developer,
            () => DevPanel.OpenAutoSlay());
    }

    internal static void RegisterCheatTabs() {
        RegisterTab("devmode.cards", "cards", "panel.cards", "Cards", 100, KitLibTabGroup.Primary, KitLibTabKind.Cheat, () => DevPanel.OpenCards());
        RegisterTab("devmode.relics", "diamond-stone", "panel.relics", "Relics", 200, KitLibTabGroup.Primary, KitLibTabKind.Cheat, () => DevPanel.OpenRelics());
        RegisterTab("devmode.enemies", "skull", "panel.enemies", "Enemies", 300, KitLibTabGroup.Primary, KitLibTabKind.Cheat, () => DevPanel.OpenEnemies());
        RegisterTab("devmode.powers", "flash", "panel.powers", "Powers", 400, KitLibTabGroup.Primary, KitLibTabKind.Cheat, () => DevPanel.OpenPowers());
        RegisterTab("devmode.potions", "flask-outline", "panel.potions", "Potions", 500, KitLibTabGroup.Primary, KitLibTabKind.Cheat, () => DevPanel.OpenPotions());
        RegisterTab("devmode.events", "calendar-star", "panel.events", "Events", 600, KitLibTabGroup.Primary, KitLibTabKind.Cheat, () => DevPanel.OpenEvents());
        RegisterTab("devmode.rooms", "map-marker", "panel.rooms", "Rooms", 650, KitLibTabGroup.Primary, KitLibTabKind.Cheat, () => DevPanel.OpenRooms());
        RegisterTab("devmode.console", "console", "panel.console", "Console", 700, KitLibTabGroup.Primary, KitLibTabKind.Cheat, () => DevPanel.OpenConsole());
        RegisterTab("devmode.cheats", "star", "panel.cheats", "Cheats", 750, KitLibTabGroup.Primary, KitLibTabKind.Cheat,
            gui => KitLibPanelUiOps.ShowCheatsOverlay?.Invoke(gui));
        RegisterTab("devmode.presets", "book-open-variant", "panel.presets", "Presets", 800, KitLibTabGroup.Primary, KitLibTabKind.Cheat, () => DevPanel.OpenPresets());
        RegisterTab("devmode.cardtest", "animation-play", "panel.cardtest", "Card Test", 850, KitLibTabGroup.Primary, KitLibTabKind.Cheat, () => DevPanel.OpenCardTest());
        RegisterTab("devmode.save", "content-save", "panel.save", "Save / Load", 100, KitLibTabGroup.Utility, KitLibTabKind.Cheat,
            gui => KitLibPanelUiOps.ShowSaveLoadOverlay?.Invoke(gui));
    }

    static void RegisterTab(
        string id,
        string iconKey,
        string displayNameKey,
        string displayNameFallback,
        int order,
        KitLibTabGroup group,
        KitLibTabKind kind,
        Action activate) =>
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = id,
            IconKey = iconKey,
            DisplayNameKey = displayNameKey,
            DisplayNameFallback = displayNameFallback,
            Order = order,
            Group = group,
            Kind = kind,
            OwningModuleId = KitLibModuleIds.Panel,
            OnActivate = _ => activate(),
        });

    static void RegisterTab(
        string id,
        string iconKey,
        string displayNameKey,
        string displayNameFallback,
        int order,
        KitLibTabGroup group,
        KitLibTabKind kind,
        Action<object> activate) =>
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = id,
            IconKey = iconKey,
            DisplayNameKey = displayNameKey,
            DisplayNameFallback = displayNameFallback,
            Order = order,
            Group = group,
            Kind = kind,
            OwningModuleId = KitLibModuleIds.Panel,
            OnActivate = activate,
        });
}
