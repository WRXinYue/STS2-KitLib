using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class ModPanelDiagnosticLogTests {
    static ModPanelOpenReport SampleOpen(
        int expectedRows = 2,
        int rawLoaded = 2,
        int catalogSnapshot = 2,
        ModPanelEmbedHostStatus embed = ModPanelEmbedHostStatus.Ready,
        bool submenuAlive = true,
        params string[] modIds) {
        var embedProbe = new ModPanelEmbedHostProbeResult(embed, "STS2-RitsuLib", "STS2RitsuLib.Settings.RitsuModSettingsSubmenu", null);
        return new(
            expectedRows,
            modIds.Length > 0 ? modIds[0] : "KitLib",
            modIds,
            rawLoaded,
            catalogSnapshot,
            embedProbe,
            submenuAlive);
    }

    [Fact]
    public void FormatOpen_includes_prefix_and_row_counts() {
        var line = ModPanelDiagnosticLog.FormatOpen(SampleOpen(modIds: ["AlphaMod", "BetaMod"]));
        Assert.StartsWith("[ModPanelDiag] open:", line, StringComparison.Ordinal);
        Assert.Contains("expectedRows=2", line, StringComparison.Ordinal);
        Assert.Contains("modIds=[AlphaMod, BetaMod]", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Stub_emitOpen_records_info_and_manifest_gap_warning() {
        var stub = new ModPanelDiagnosticLogStub();
        stub.EmitOpen(SampleOpen(expectedRows: 0, rawLoaded: 3, catalogSnapshot: 0, modIds: []));
        Assert.Single(stub.InfoLines);
        Assert.Contains("expectedRows=0", stub.InfoLines[0], StringComparison.Ordinal);
        Assert.Contains("rawLoaded=3", stub.InfoLines[0], StringComparison.Ordinal);
        Assert.Single(stub.WarnLines);
        Assert.Contains("catalog snapshot is empty", stub.WarnLines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Stub_emitOpen_records_embed_warning_when_rows_expected() {
        var stub = new ModPanelDiagnosticLogStub();
        stub.EmitOpen(SampleOpen(
            expectedRows: 2,
            embed: ModPanelEmbedHostStatus.SubmenuTypeNotFound,
            modIds: ["AlphaMod", "BetaMod"]));
        Assert.Single(stub.WarnLines);
        Assert.Contains("embed host is SubmenuTypeNotFound", stub.WarnLines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLayout_reports_missing_scroll() {
        var layout = new ModPanelLayoutSnapshot("(640, 480)", null, null, null, null, 0, ScrollMissing: true);
        var line = ModPanelDiagnosticLog.FormatLayout(layout);
        Assert.Contains("scroll=missing", line, StringComparison.Ordinal);
        Assert.Contains("modSections=0", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Stub_emitLayout_warns_when_sections_missing_despite_plan() {
        var stub = new ModPanelDiagnosticLogStub();
        var open = SampleOpen(expectedRows: 3, modIds: ["A", "B", "C"]);
        var layout = new ModPanelLayoutSnapshot(
            "(640, 480)", "(200, 300)", true, 0, 0, 0, ScrollMissing: false);
        stub.EmitLayout(open, layout);
        Assert.Single(stub.InfoLines);
        Assert.Contains("modSections=0", stub.InfoLines[0], StringComparison.Ordinal);
        Assert.Contains(stub.WarnLines, w => w.Contains("built 0 mod sections", StringComparison.Ordinal));
    }

    [Fact]
    public void Stub_emitLayout_warns_on_row_count_mismatch() {
        var stub = new ModPanelDiagnosticLogStub();
        var open = SampleOpen(expectedRows: 3, modIds: ["A", "B", "C"]);
        var layout = new ModPanelLayoutSnapshot(
            "(640, 480)", "(200, 300)", true, 2, 2, 2, ScrollMissing: false);
        stub.EmitLayout(open, layout);
        Assert.Contains(stub.WarnLines, w => w.Contains("row count mismatch", StringComparison.Ordinal));
        Assert.Contains(stub.WarnLines, w => w.Contains("plan=3", StringComparison.Ordinal));
        Assert.Contains(stub.WarnLines, w => w.Contains("uiSections=2", StringComparison.Ordinal));
    }

    [Fact]
    public void CollectLayoutWarnings_flags_invisible_scroll() {
        var open = SampleOpen(expectedRows: 1, modIds: ["A"]);
        var layout = new ModPanelLayoutSnapshot(
            "(640, 480)", "(200, 0)", false, 1, 1, 1, ScrollMissing: false);
        var warnings = ModPanelDiagnosticLog.CollectLayoutWarnings(open, layout);
        Assert.Contains(warnings, w => w.Contains("scroll container is not visible", StringComparison.Ordinal));
    }
}
