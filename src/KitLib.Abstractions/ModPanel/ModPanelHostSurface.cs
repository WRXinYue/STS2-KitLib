namespace KitLib.Abstractions.ModPanel;

[Flags]
public enum ModPanelHostSurface {
    None = 0,
    MainMenu = 1 << 0,
    RunPause = 1 << 1,
    CombatPause = 1 << 2,
    All = MainMenu | RunPause | CombatPause,
}

public static class ModPanelHostSurfaceRules {
    public static bool IsInRun(ModPanelHostSurface surface) =>
        surface is ModPanelHostSurface.RunPause or ModPanelHostSurface.CombatPause;

    public static bool IsModLoadEditingAllowed(ModPanelHostSurface surface) =>
        surface == ModPanelHostSurface.MainMenu;
}
