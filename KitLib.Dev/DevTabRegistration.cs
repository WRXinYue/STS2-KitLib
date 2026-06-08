using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Icons;

namespace KitLib.Dev;

internal static class DevTabRegistration {
    internal static void Register() {
        RegisterActionTab("devmode.enemyIntent", MdiIcon.From("bullseye-arrow"), I18N.T("panel.enemyIntent", "Enemy intents"), 754, () => KitLibDevOps.OpenEnemyIntent?.Invoke(), KitLibTabKind.Developer);
        RegisterActionTab("devmode.combatStats", MdiIcon.From("chart-bar"), I18N.T("panel.combatStats", "Combat Stats"), 756, () => KitLibDevOps.OpenCombatStats?.Invoke(), KitLibTabKind.Developer);
        RegisterActionTab("devmode.hooks", MdiIcon.From("lightning-bolt"), I18N.T("panel.hooks", "Hooks"), 900, () => KitLibDevOps.OpenHooks?.Invoke());
        RegisterActionTab("devmode.scripts", MdiIcon.PuzzleOutline, I18N.T("panel.scripts", "Scripts"), 950, () => KitLibDevOps.OpenScripts?.Invoke());
        RegisterActionTab("devmode.harmonyAnalysis", MdiIcon.Magnify, I18N.T("panel.harmonyAnalysis", "Harmony analysis"), 962, () => KitLibDevOps.OpenHarmonyAnalysis?.Invoke(), KitLibTabKind.Developer);
        RegisterActionTab("devmode.frameworks", MdiIcon.FilterVariant, I18N.T("panel.frameworks", "Frameworks"), 965, () => KitLibDevOps.OpenFrameworks?.Invoke(), KitLibTabKind.Developer);
        RegisterActionTab("devmode.feedback", MdiIcon.From("bug-outline"), I18N.T("panel.feedback", "Mod Feedback"), 970, () => KitLibDevOps.OpenFeedback?.Invoke(), KitLibTabKind.Developer);
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = "devmode.settings",
            IconKey = MdiIcon.From("cog").Name,
            DisplayName = I18N.T("panel.settings", "Settings"),
            Order = 200,
            Group = KitLibTabGroup.Utility,
            Kind = KitLibTabKind.Developer,
            OwningModuleId = KitLibModuleIds.Dev,
            OnActivate = gui => KitLibPanelUiOps.ShowSettingsOverlay?.Invoke(gui),
        });
    }

    static void RegisterActionTab(string id, MdiIcon icon, string displayName, int order, Action activate, KitLibTabKind kind = KitLibTabKind.Cheat) =>
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = id,
            IconKey = icon.Name,
            DisplayName = displayName,
            Order = order,
            Group = KitLibTabGroup.Primary,
            Kind = kind,
            OwningModuleId = KitLibModuleIds.Dev,
            OnActivate = _ => activate(),
        });
}
