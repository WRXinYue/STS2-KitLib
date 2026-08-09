using KitLib.Abstractions.Host;

namespace KitLib.Abstractions.Tests;

public sealed class SatelliteModuleLoadPolicyTests {
    [Fact]
    public void Default_toggles_enable_panel_only() {
        var defaults = SatelliteModuleLoadPolicy.GetDefaultToggles(mobileDefaults: false);
        Assert.True(defaults[KitLibModuleIds.Panel]);
        Assert.False(defaults[KitLibModuleIds.Ai]);
        Assert.False(defaults[KitLibModuleIds.Dev]);
    }

    [Fact]
    public void Default_toggles_enable_all_optional_modules_on_mobile() {
        var defaults = SatelliteModuleLoadPolicy.GetDefaultToggles(mobileDefaults: true);
        Assert.True(defaults[KitLibModuleIds.Panel]);
        Assert.True(defaults[KitLibModuleIds.Ai]);
        Assert.True(defaults[KitLibModuleIds.Dev]);
    }

    [Fact]
    public void ResolveEnabled_does_not_include_product_entry_modpanel() {
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(
            SatelliteModuleLoadPolicy.GetDefaultToggles());
        Assert.False(resolved.ContainsKey(KitLibModuleIds.ModPanel));
        Assert.True(resolved[KitLibModuleIds.Panel]);
        Assert.False(resolved[KitLibModuleIds.Ai]);
        Assert.False(resolved.ContainsKey(KitLibModuleIds.User));
        Assert.False(resolved.ContainsKey(KitLibModuleIds.Cheat));
    }

    [Fact]
    public void ResolveEnabled_uses_user_toggles_when_provided() {
        var toggles = new Dictionary<string, bool> {
            [KitLibModuleIds.Panel] = true,
            [KitLibModuleIds.Ai] = true,
            [KitLibModuleIds.Dev] = false,
        };
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(toggles);
        Assert.True(resolved[KitLibModuleIds.Panel]);
        Assert.True(resolved[KitLibModuleIds.Ai]);
        Assert.False(resolved[KitLibModuleIds.Dev]);
    }

    [Fact]
    public void ResolveEnabled_skips_dev_when_panel_off_but_keeps_ai() {
        var toggles = new Dictionary<string, bool> {
            [KitLibModuleIds.Panel] = false,
            [KitLibModuleIds.Ai] = true,
            [KitLibModuleIds.Dev] = true,
        };
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(toggles);
        Assert.True(resolved[KitLibModuleIds.Ai]);
        Assert.False(resolved[KitLibModuleIds.Dev]);
    }

    [Fact]
    public void ApplyDependencyRulesToToggles_enabling_dev_enables_panel() {
        var toggles = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) {
            [KitLibModuleIds.Panel] = false,
            [KitLibModuleIds.Ai] = false,
            [KitLibModuleIds.Dev] = false,
        };
        SatelliteModuleLoadPolicy.ApplyDependencyRulesToToggles(toggles, KitLibModuleIds.Dev, enabled: true);
        Assert.True(toggles[KitLibModuleIds.Panel]);
        Assert.True(toggles[KitLibModuleIds.Dev]);
    }

    [Fact]
    public void ShouldLoad_requires_dll_and_enabled_flag() {
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(null);
        Assert.True(SatelliteModuleLoadPolicy.ShouldLoad(KitLibModuleIds.Panel, resolved, dllExists: true));
        Assert.False(SatelliteModuleLoadPolicy.ShouldLoad(KitLibModuleIds.Panel, resolved, dllExists: false));
        Assert.False(SatelliteModuleLoadPolicy.ShouldLoad(KitLibModuleIds.Ai, resolved, dllExists: true));
    }

    [Fact]
    public void GetDependents_lists_dev_for_panel() {
        var dependents = SatelliteModuleLoadPolicy.GetDependents(KitLibModuleIds.Panel);
        Assert.DoesNotContain(KitLibModuleIds.Ai, dependents);
        Assert.Contains(KitLibModuleIds.Dev, dependents);
    }

    [Fact]
    public void IsKnownSatellite_excludes_product_entry_and_in_process_modules() {
        Assert.False(SatelliteModuleLoadPolicy.IsKnownSatellite(KitLibModuleIds.ModPanel));
        Assert.False(SatelliteModuleLoadPolicy.IsKnownSatellite(KitLibModuleIds.User));
        Assert.False(SatelliteModuleLoadPolicy.IsKnownSatellite(KitLibModuleIds.Cheat));
        Assert.False(SatelliteModuleLoadPolicy.IsToggleable(KitLibModuleIds.Cheat));
        Assert.True(SatelliteModuleLoadPolicy.IsKnownSatellite(KitLibModuleIds.Panel));
    }

    [Fact]
    public void ResolveEnabled_keeps_ai_when_panel_off() {
        var toggles = new Dictionary<string, bool> {
            [KitLibModuleIds.Panel] = false,
            [KitLibModuleIds.Ai] = true,
            [KitLibModuleIds.Dev] = false,
        };
        var resolved = SatelliteModuleLoadPolicy.ResolveEnabled(toggles);
        Assert.True(resolved[KitLibModuleIds.Ai]);
    }

    [Fact]
    public void GetRelativeDllPath_uses_modules_subdir() {
        Assert.Equal("modules/KitLib.Panel.dll", SatelliteModuleLoadPolicy.GetRelativeDllPath(KitLibModuleIds.Panel));
    }
}
