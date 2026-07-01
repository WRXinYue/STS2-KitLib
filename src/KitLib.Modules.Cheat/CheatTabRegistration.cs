using KitLib.Abstractions.Host;
using KitLib.Host;

namespace KitLib.Cheat;

internal static class CheatTabRegistration {
    internal static void Register() {
        RegisterTab("devmode.cards", "cards", I18N.T("panel.cards", "Cards"), 100, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenCards?.Invoke());
        RegisterTab("devmode.relics", "diamond-stone", I18N.T("panel.relics", "Relics"), 200, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenRelics?.Invoke());
        RegisterTab("devmode.enemies", "skull", I18N.T("panel.enemies", "Enemies"), 300, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenEnemies?.Invoke());
        RegisterTab("devmode.powers", "flash", I18N.T("panel.powers", "Powers"), 400, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenPowers?.Invoke());
        RegisterTab("devmode.potions", "flask-outline", I18N.T("panel.potions", "Potions"), 500, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenPotions?.Invoke());
        RegisterTab("devmode.events", "calendar-star", I18N.T("panel.events", "Events"), 600, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenEvents?.Invoke());
        RegisterTab("devmode.rooms", "map-marker", I18N.T("panel.rooms", "Rooms"), 650, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenRooms?.Invoke());
        RegisterTab("devmode.console", "console", I18N.T("panel.console", "Console"), 700, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenConsole?.Invoke());
        RegisterTab("devmode.cheats", "star", I18N.T("panel.cheats", "Cheats"), 750, KitLibTabGroup.Primary,
            gui => KitLibPanelUiOps.ShowCheatsOverlay?.Invoke(gui));
        RegisterTab("devmode.presets", "book-open-variant", I18N.T("panel.presets", "Presets"), 800, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenPresets?.Invoke());
        RegisterTab("devmode.cardtest", "animation-play", I18N.T("panel.cardtest", "Card Test"), 850, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenCardTest?.Invoke());
        RegisterTab("devmode.save", "content-save", I18N.T("panel.save", "Save / Load"), 100, KitLibTabGroup.Utility,
            gui => KitLibPanelUiOps.ShowSaveLoadOverlay?.Invoke(gui));
    }

    static void RegisterTab(string id, string iconKey, string displayName, int order, KitLibTabGroup group, Action activate) =>
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = id,
            IconKey = iconKey,
            DisplayName = displayName,
            Order = order,
            Group = group,
            Kind = KitLibTabKind.Cheat,
            OwningModuleId = KitLibModuleIds.Cheat,
            OnActivate = _ => activate(),
        });

    static void RegisterTab(string id, string iconKey, string displayName, int order, KitLibTabGroup group, Action<object> activate) =>
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = id,
            IconKey = iconKey,
            DisplayName = displayName,
            Order = order,
            Group = group,
            Kind = KitLibTabKind.Cheat,
            OwningModuleId = KitLibModuleIds.Cheat,
            OnActivate = activate,
        });
}
