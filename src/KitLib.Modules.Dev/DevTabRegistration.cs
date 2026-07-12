using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Host;

namespace KitLib.Dev;

internal static class DevTabRegistration {
    internal static void Register() {
        RegisterActionTab("devmode.enemyIntent", "bullseye-arrow", I18N.T("panel.enemyIntent", "Enemy intents"), 754, () => KitLibDevOps.OpenEnemyIntent?.Invoke(), KitLibTabKind.Developer);
        RegisterActionTab("devmode.combatStats", "chart-bar", I18N.T("panel.combatStats", "Combat Stats"), 756, () => KitLibDevOps.OpenCombatStats?.Invoke(), KitLibTabKind.Developer);
        RegisterActionTab("devmode.hooks", "lightning-bolt", I18N.T("panel.hooks", "Hooks"), 900, () => KitLibDevOps.OpenHooks?.Invoke());
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = "devmode.settings",
            IconKey = "cog",
            DisplayName = I18N.T("panel.settings", "Settings"),
            Order = 200,
            Group = KitLibTabGroup.Utility,
            Kind = KitLibTabKind.Developer,
            OwningModuleId = KitLibModuleIds.Dev,
            OnActivate = gui => KitLibPanelUiOps.ShowSettingsOverlay?.Invoke(gui),
        });
    }

    static void RegisterActionTab(string id, string iconKey, string displayName, int order, Action activate, KitLibTabKind kind = KitLibTabKind.Cheat) =>
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = id,
            IconKey = iconKey,
            DisplayName = displayName,
            Order = order,
            Group = KitLibTabGroup.Primary,
            Kind = kind,
            OwningModuleId = KitLibModuleIds.Dev,
            OnActivate = _ => activate(),
        });
}
