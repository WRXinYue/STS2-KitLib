using KitLib.Abstractions.ModPanel;

namespace KitLib.ModPanel.Tests;

public sealed class ModPanelHostSurfaceRulesTests {
    [Theory]
    [InlineData(ModPanelHostSurface.MainMenu, false)]
    [InlineData(ModPanelHostSurface.RunPause, true)]
    [InlineData(ModPanelHostSurface.CombatPause, true)]
    public void IsInRun_matches_surface(ModPanelHostSurface surface, bool expected) =>
        Assert.Equal(expected, ModPanelHostSurfaceRules.IsInRun(surface));

    [Theory]
    [InlineData(ModPanelHostSurface.MainMenu, true)]
    [InlineData(ModPanelHostSurface.RunPause, false)]
    [InlineData(ModPanelHostSurface.CombatPause, false)]
    public void IsModLoadEditingAllowed_only_on_main_menu(ModPanelHostSurface surface, bool expected) =>
        Assert.Equal(expected, ModPanelHostSurfaceRules.IsModLoadEditingAllowed(surface));
}
