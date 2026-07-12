using KitLib.Abstractions.Host;
using KitLib.Host;

namespace KitLib.User;

internal static class UserTabRegistration {
    internal static void Register() {
        KitLibHost.RegisterTab(new KitLibTabDescriptor {
            Id = "devmode.logs",
            IconKey = "text-box-outline",
            DisplayNameKey = "panel.logs",
            DisplayNameFallback = "Logs",
            Order = 900,
            Group = KitLibTabGroup.Utility,
            Kind = KitLibTabKind.Developer,
            OwningModuleId = KitLibModuleIds.User,
            OnActivate = _ => KitLibUserOps.OpenLogs?.Invoke(),
        });
    }
}
