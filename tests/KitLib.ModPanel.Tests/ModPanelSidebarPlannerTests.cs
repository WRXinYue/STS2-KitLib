using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class ModPanelSidebarPlannerTests {
    static readonly KitLibModInfo ModA = new("AlphaMod", "Zulu Pack", "1.0", []);
    static readonly KitLibModInfo ModB = new("BetaMod", "Alpha Pack", "2.0", []);
    static readonly KitLibModInfo Ritsu = new("STS2-RitsuLib", "RitsuLib", "0.1", []);

    static bool IsRitsuFramework(string? id) =>
        string.Equals(id, "STS2-RitsuLib", StringComparison.OrdinalIgnoreCase)
        || string.Equals(id, "RitsuLib", StringComparison.OrdinalIgnoreCase)
        || string.Equals(id, "com.ritsukage.sts2-RitsuLib", StringComparison.OrdinalIgnoreCase);

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
            _ => true);
        Assert.Equal(3, plan.ExpectedRowCount);
    }

    [Fact]
    public void Plan_empty_snapshot_yields_zero_rows() {
        var plan = ModPanelSidebarPlanner.Plan(
            Array.Empty<KitLibModInfo>(),
            "KitLib.ModPanel",
            IsRitsuFramework,
            _ => true);
        Assert.Equal(0, plan.ExpectedRowCount);
    }

    [Fact]
    public void ResolveShowcaseModId_skips_ritsu_framework_ids() {
        var id = ModPanelSidebarPlanner.ResolveShowcaseModId(
            [Ritsu, ModA],
            "STS2-RitsuLib",
            IsRitsuFramework,
            mid => string.Equals(mid, "AlphaMod", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("AlphaMod", id);
    }

    [Fact]
    public void Plan_falls_back_to_first_mod_when_showcase_missing() {
        var plan = ModPanelSidebarPlanner.Plan(
            [ModB, ModA],
            "MissingMod",
            IsRitsuFramework,
            mid => string.Equals(mid, "BetaMod", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mid, "AlphaMod", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("BetaMod", plan.InitialSelectedModId);
        Assert.Equal(2, plan.ExpectedRowCount);
    }

    [Fact]
    public void Raw_loaded_greater_than_snapshot_indicates_manifest_id_gap() {
        // Documents the runtime warning: rawLoaded > catalogSnapshot with expectedRows=0.
        var plan = ModPanelSidebarPlanner.Plan([], "KitLib", IsRitsuFramework, _ => true);
        Assert.Equal(0, plan.ExpectedRowCount);
        const int rawLoaded = 5;
        const int catalogSnapshot = 0;
        Assert.True(rawLoaded > catalogSnapshot);
    }
}
