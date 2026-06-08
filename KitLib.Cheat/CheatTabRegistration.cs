using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Icons;

namespace KitLib.Cheat;

internal static class CheatTabRegistration {
    internal static void Register() {
        RegisterTab("devmode.cards", MdiIcon.From("cards"), I18N.T("panel.cards", "Cards"), 100, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenCards?.Invoke());
        RegisterTab("devmode.relics", MdiIcon.From("diamond-stone"), I18N.T("panel.relics", "Relics"), 200, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenRelics?.Invoke());
        RegisterTab("devmode.enemies", MdiIcon.Skull, I18N.T("panel.enemies", "Enemies"), 300, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenEnemies?.Invoke());
        RegisterTab("devmode.powers", MdiIcon.From("flash"), I18N.T("panel.powers", "Powers"), 400, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenPowers?.Invoke());
        RegisterTab("devmode.potions", MdiIcon.From("flask-outline"), I18N.T("panel.potions", "Potions"), 500, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenPotions?.Invoke());
        RegisterTab("devmode.events", MdiIcon.CalendarStar, I18N.T("panel.events", "Events"), 600, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenEvents?.Invoke());
        RegisterTab("devmode.rooms", MdiIcon.From("map-marker"), I18N.T("panel.rooms", "Rooms"), 650, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenRooms?.Invoke());
        RegisterTab("devmode.console", MdiIcon.Console, I18N.T("panel.console", "Console"), 700, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenConsole?.Invoke());
        RegisterTab("devmode.cheats", MdiIcon.Star, I18N.T("panel.cheats", "Cheats"), 750, KitLibTabGroup.Primary,
            gui => KitLibPanelUiOps.ShowCheatsOverlay?.Invoke(gui));
        RegisterTab("devmode.presets", MdiIcon.From("book-open-variant"), I18N.T("panel.presets", "Presets"), 800, KitLibTabGroup.Primary, () => KitLibCheatOps.OpenPresets?.Invoke());
        RegisterTab("devmode.save", MdiIcon.ContentSave, I18N.T("panel.save", "Save / Load"), 100, KitLibTabGroup.Utility,
            gui => KitLibPanelUiOps.ShowSaveLoadOverlay?.Invoke(gui));
    }

    static void RegisterTab(string id, MdiIcon icon, string displayName, int order, KitLibTabGroup group, Action activate) =>
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = id,
            IconKey = icon.Name,
            DisplayName = displayName,
            Order = order,
            Group = group,
            Kind = KitLibTabKind.Cheat,
            OwningModuleId = KitLibModuleIds.Cheat,
            OnActivate = _ => activate(),
        });

    static void RegisterTab(string id, MdiIcon icon, string displayName, int order, KitLibTabGroup group, Action<object> activate) =>
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = id,
            IconKey = icon.Name,
            DisplayName = displayName,
            Order = order,
            Group = group,
            Kind = KitLibTabKind.Cheat,
            OwningModuleId = KitLibModuleIds.Cheat,
            OnActivate = activate,
        });
}
