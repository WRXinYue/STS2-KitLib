using KitLib.Abstractions.ModPanel;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

internal static class ModPanelHostContext {
    public static ModPanelHostSurface ActiveSurface { get; private set; } = ModPanelHostSurface.MainMenu;

    public static bool IsInRun => ModPanelHostSurfaceRules.IsInRun(ActiveSurface);

    public static bool IsModLoadEditingAllowed =>
        ModPanelHostSurfaceRules.IsModLoadEditingAllowed(ActiveSurface);

    internal static void SetActiveSurface(ModPanelHostSurface surface) =>
        ActiveSurface = surface;

    public static ModPanelHostSurface ResolveCurrent() {
        if (RunManager.Instance?.IsInProgress != true)
            return ModPanelHostSurface.MainMenu;

        return CombatManager.Instance?.IsInProgress == true
            ? ModPanelHostSurface.CombatPause
            : ModPanelHostSurface.RunPause;
    }
}
