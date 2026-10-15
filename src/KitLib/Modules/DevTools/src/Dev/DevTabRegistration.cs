using KitLib.Abstractions.Host;
using KitLib.Host;

namespace KitLib.Dev;

internal static class DevTabRegistration {
    internal static void Register() {
        RegisterActionTab("devmode.enemyIntent", "bullseye-arrow", "panel.enemyIntent", "Enemy intents", 754, () => KitLibDevOps.OpenEnemyIntent?.Invoke(), KitLibTabKind.Developer);
        RegisterActionTab("devmode.hooks", "lightning-bolt", "panel.hooks", "Hooks", 900, () => KitLibDevOps.OpenHooks?.Invoke());
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = "devmode.settings",
            IconKey = "cog",
            DisplayNameKey = "panel.settings",
            DisplayNameFallback = "Settings",
            Order = 200,
            Group = KitLibTabGroup.Utility,
            Kind = KitLibTabKind.Developer,
            OwningModuleId = KitLibModuleIds.Dev,
            OnActivate = gui => KitLibPanelUiOps.ShowSettingsOverlay?.Invoke(gui),
        });
    }

    static void RegisterActionTab(string id, string iconKey, string displayNameKey, string displayNameFallback, int order, Action activate, KitLibTabKind kind = KitLibTabKind.Cheat) =>
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = id,
            IconKey = iconKey,
            DisplayNameKey = displayNameKey,
            DisplayNameFallback = displayNameFallback,
            Order = order,
            Group = KitLibTabGroup.Primary,
            Kind = kind,
            OwningModuleId = KitLibModuleIds.Dev,
            OnActivate = _ => activate(),
        });
}
