using KitLib.Abstractions.Host;

namespace KitLib.Abstractions.Tests;

public sealed class SatelliteModuleLoadPolicyTests {
    [Fact]
    public void Default_toggles_enable_panel_only() {
        var defaults = SatelliteModuleLoadPolicy.GetDefaultToggles();
        Assert.True(defaults[KitLibModuleIds.Panel]);
        Assert.False(defaults[KitLibModuleIds.Ai]);
        Assert.False(defaults[KitLibModuleIds.Cheat]);
        Assert.False(defaults[KitLibModuleIds.Dev]);
    }

    [Fact]
    public void ResolveEnabled_keeps_user_and_modpanel_always_on() {
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(
            SatelliteModuleLoadPolicy.GetDefaultToggles());
        Assert.True(resolved[KitLibModuleIds.User]);
        Assert.True(resolved[KitLibModuleIds.ModPanel]);
        Assert.True(resolved[KitLibModuleIds.Panel]);
        Assert.False(resolved[KitLibModuleIds.Ai]);
    }

    [Fact]
    public void ResolveEnabled_uses_user_toggles_when_provided() {
        var toggles = new Dictionary<string, bool> {
            [KitLibModuleIds.Panel] = true,
            [KitLibModuleIds.Ai] = true,
            [KitLibModuleIds.Cheat] = false,
            [KitLibModuleIds.Dev] = false,
        };
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(toggles);
        Assert.True(resolved[KitLibModuleIds.Panel]);
        Assert.True(resolved[KitLibModuleIds.Ai]);
        Assert.False(resolved[KitLibModuleIds.Cheat]);
    }

    [Fact]
    public void ResolveEnabled_skips_cheat_and_dev_when_panel_off() {
        var toggles = new Dictionary<string, bool> {
            [KitLibModuleIds.Panel] = false,
            [KitLibModuleIds.Ai] = false,
            [KitLibModuleIds.Cheat] = true,
            [KitLibModuleIds.Dev] = true,
        };
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(toggles);
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
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(null);
        Assert.True(SatelliteModuleLoadPolicy.ShouldLoad(KitLibModuleIds.Panel, resolved, dllExists: true));
        Assert.False(SatelliteModuleLoadPolicy.ShouldLoad(KitLibModuleIds.Panel, resolved, dllExists: false));
        Assert.False(SatelliteModuleLoadPolicy.ShouldLoad(KitLibModuleIds.Ai, resolved, dllExists: true));
    }

    [Fact]
    public void GetDependents_lists_modules_that_require_panel() {
        var dependents = SatelliteModuleLoadPolicy.GetDependents(KitLibModuleIds.Panel);
        Assert.Contains(KitLibModuleIds.Cheat, dependents);
        Assert.Contains(KitLibModuleIds.Dev, dependents);
        Assert.DoesNotContain(KitLibModuleIds.Ai, dependents);
    }

    [Fact]
    public void GetRelativeDllPath_uses_modules_subdir() {
        Assert.Equal("modules/KitLib.Panel.dll", SatelliteModuleLoadPolicy.GetRelativeDllPath(KitLibModuleIds.Panel));
    }
}
