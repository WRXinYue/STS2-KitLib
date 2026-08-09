using KitLib.Abstractions.Host;
using KitLib.Host;

namespace KitLib.AI;

internal static class AiTabRegistration {
    internal static void Register() {
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = "devmode.ai",
            IconKey = "robot",
            DisplayNameKey = "panel.ai",
            DisplayNameFallback = "AI Host",
            Order = 745,
            Group = KitLibTabGroup.Primary,
            Kind = KitLibTabKind.Cheat,
            OwningModuleId = KitLibModuleIds.Ai,
            OnActivate = gui => KitLibPanelUiOps.ShowAiOverlay?.Invoke(gui),
        });
    }
}
