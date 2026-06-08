using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Icons;

namespace KitLib.AI;

internal static class AiTabRegistration {
    internal static void Register() {
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = "devmode.ai",
            IconKey = MdiIcon.Robot.Name,
            DisplayName = I18N.T("panel.ai", "AI Host"),
            Order = 745,
            Group = KitLibTabGroup.Primary,
            Kind = KitLibTabKind.Cheat,
            OwningModuleId = KitLibModuleIds.Ai,
            OnActivate = gui => KitLibPanelUiOps.ShowAiOverlay?.Invoke(gui),
        });
    }
}
