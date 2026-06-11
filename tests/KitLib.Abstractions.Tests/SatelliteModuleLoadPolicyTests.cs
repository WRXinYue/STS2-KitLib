using KitLib.Abstractions.Host;

namespace KitLib.Abstractions.Tests;

public sealed class SatelliteModuleLoadPolicyTests {
    [Fact]
    public void Standard_preset_enables_panel_only() {
        var preset = SatelliteModuleLoadPolicy.GetPreset(SatelliteModuleLoadPolicy.ProfileStandard);
        Assert.True(preset[KitLibModuleIds.Panel]);
        Assert.False(preset[KitLibModuleIds.Ai]);
        Assert.False(preset[KitLibModuleIds.Cheat]);
        Assert.False(preset[KitLibModuleIds.Dev]);
    }

    [Fact]
    public void Full_preset_enables_all_toggleable_modules() {
        var preset = SatelliteModuleLoadPolicy.GetPreset(SatelliteModuleLoadPolicy.ProfileFull);
        Assert.True(preset[KitLibModuleIds.Panel]);
        Assert.True(preset[KitLibModuleIds.Ai]);
        Assert.True(preset[KitLibModuleIds.Cheat]);
        Assert.True(preset[KitLibModuleIds.Dev]);
    }

    [Fact]
    public void ResolveEnabled_keeps_user_and_modpanel_always_on() {
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(
            SatelliteModuleLoadPolicy.ProfileMinimal,
            SatelliteModuleLoadPolicy.GetPreset(SatelliteModuleLoadPolicy.ProfileMinimal));
        Assert.True(resolved[KitLibModuleIds.User]);
        Assert.True(resolved[KitLibModuleIds.ModPanel]);
        Assert.False(resolved[KitLibModuleIds.Panel]);
    }

    [Fact]
    public void ResolveEnabled_skips_cheat_and_dev_when_panel_off() {
        var toggles = new Dictionary<string, bool> {
            [KitLibModuleIds.Panel] = false,
            [KitLibModuleIds.Ai] = false,
            [KitLibModuleIds.Cheat] = true,
            [KitLibModuleIds.Dev] = true,
        };
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(SatelliteModuleLoadPolicy.ProfileCustom, toggles);
        Assert.False(resolved[KitLibModuleIds.Cheat]);
        Assert.False(resolved[KitLibModuleIds.Dev]);
    }

    [Fact]
    public void ApplyDependencyRulesToToggles_enabling_cheat_enables_panel() {
        var toggles = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) {
            [KitLibModuleIds.Panel] = false,
            [KitLibModuleIds.Cheat] = false,
            [KitLibModuleIds.Dev] = false,
        };
        SatelliteModuleLoadPolicy.ApplyDependencyRulesToToggles(toggles, KitLibModuleIds.Cheat, enabled: true);
        Assert.True(toggles[KitLibModuleIds.Panel]);
        Assert.True(toggles[KitLibModuleIds.Cheat]);
    }

    [Fact]
    public void ShouldLoad_requires_dll_and_enabled_flag() {
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(
            SatelliteModuleLoadPolicy.ProfileStandard,
            null);
        Assert.True(SatelliteModuleLoadPolicy.ShouldLoad(KitLibModuleIds.Panel, resolved, dllExists: true));
        Assert.False(SatelliteModuleLoadPolicy.ShouldLoad(KitLibModuleIds.Panel, resolved, dllExists: false));
        Assert.False(SatelliteModuleLoadPolicy.ShouldLoad(KitLibModuleIds.Ai, resolved, dllExists: true));
    }

    [Fact]
    public void DetectMatchingProfile_returns_custom_for_mixed_toggles() {
        var toggles = new Dictionary<string, bool> {
            [KitLibModuleIds.Panel] = true,
            [KitLibModuleIds.Ai] = true,
            [KitLibModuleIds.Cheat] = false,
            [KitLibModuleIds.Dev] = false,
        };
        Assert.Equal(SatelliteModuleLoadPolicy.ProfileCustom, SatelliteModuleLoadPolicy.DetectMatchingProfile(toggles));
    }
}
