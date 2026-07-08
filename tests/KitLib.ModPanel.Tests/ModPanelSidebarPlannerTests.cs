using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class ModPanelSidebarPlannerTests {
    static KitLibModEntry ModA => Entry("AlphaMod", "Zulu Pack");
    static KitLibModEntry ModB => Entry("BetaMod", "Alpha Pack");
    static KitLibModEntry Ritsu => Entry("STS2-RitsuLib", "RitsuLib");
    static KitLibModEntry DisabledMod => Entry("OffMod", "Off", ModEntryLoadStatus.Disabled, enabled: false);

    static KitLibModEntry Entry(string id, string name, ModEntryLoadStatus status = ModEntryLoadStatus.Loaded,
        bool enabled = true)
        => new(id, name, "1.0", [], status, ModEntrySource.ModsDirectory, enabled, null);

    static bool IsRitsuFramework(string? id) =>
        string.Equals(id, "STS2-RitsuLib", StringComparison.OrdinalIgnoreCase)
        || string.Equals(id, "RitsuLib", StringComparison.OrdinalIgnoreCase)
        || string.Equals(id, "com.ritsukage.sts2-RitsuLib", StringComparison.OrdinalIgnoreCase);

    static bool IsLoaded(KitLibModEntry e) => e.IsLoaded;

    [Fact]
    public void OrderForSidebar_sorts_by_display_name() {
        var ordered = ModPanelSidebarPlanner.OrderForSidebar([ModA, ModB]);
        Assert.Equal("BetaMod", ordered[0].Id);
        Assert.Equal("AlphaMod", ordered[1].Id);
    }

    [Fact]
    public void Plan_returns_row_count_matching_snapshot() {
        var plan = ModPanelSidebarPlanner.Plan(
            [ModA, ModB, Ritsu],
            "KitLib.ModPanel",
            IsRitsuFramework,
            IsLoaded);
        Assert.Equal(3, plan.ExpectedRowCount);
    }

    [Fact]
    public void Plan_empty_snapshot_yields_zero_rows() {
        var plan = ModPanelSidebarPlanner.Plan(
            Array.Empty<KitLibModEntry>(),
            "KitLib.ModPanel",
            IsRitsuFramework,
            IsLoaded);
        Assert.Equal(0, plan.ExpectedRowCount);
    }

    [Fact]
    public void ResolveShowcaseModId_skips_ritsu_framework_ids() {
        var id = ModPanelSidebarPlanner.ResolveShowcaseModId(
            [Ritsu, ModA],
            "STS2-RitsuLib",
            IsRitsuFramework,
            IsLoaded);
        Assert.Equal("AlphaMod", id);
    }

    [Fact]
    public void Plan_falls_back_to_first_mod_when_showcase_missing() {
        var plan = ModPanelSidebarPlanner.Plan(
            [ModB, ModA],
            "MissingMod",
            IsRitsuFramework,
            IsLoaded);
        Assert.Equal("BetaMod", plan.InitialSelectedModId);
        Assert.Equal(2, plan.ExpectedRowCount);
    }

    [Fact]
    public void Plan_includes_disabled_mods_in_row_count() {
        var plan = ModPanelSidebarPlanner.Plan(
            [ModA, DisabledMod],
            "KitLib",
            IsRitsuFramework,
            IsLoaded);
        Assert.Equal(2, plan.ExpectedRowCount);
        Assert.Equal("AlphaMod", plan.InitialSelectedModId);
    }
}
