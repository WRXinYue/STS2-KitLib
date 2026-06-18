using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class KitLibCompatStartupScanTests {
    static KitLibModEntry Entry(string id, ModEntryLoadStatus status, bool enabled = true)
        => new(id, id, "1.0.0", [], status, ModEntrySource.ModsDirectory, enabled);

    static KitLibCompatResult IncompatibleGame(string range) => new() {
        HasSidecar = true,
        Flags = KitLibCompatFlags.GameVersionMismatch,
        GameVersionRanges = [range],
    };

    [Fact]
    public void Collect_skips_loaded_and_disabled_mods() {
        var issues = KitLibCompatStartupScan.Collect(
            [
                Entry("Loaded", ModEntryLoadStatus.Loaded),
                Entry("Disabled", ModEntryLoadStatus.Failed, enabled: false),
                Entry("NoSidecar", ModEntryLoadStatus.Failed),
            ],
            id => id switch {
                "NoSidecar" => new KitLibCompatResult(),
                _ => IncompatibleGame("=0.103.3"),
            });
        Assert.Empty(issues);
    }

    [Fact]
    public void Collect_includes_enabled_failed_mod_with_compat_sidecar() {
        var issues = KitLibCompatStartupScan.Collect(
            [Entry("LustTravel2", ModEntryLoadStatus.Failed)],
            _ => IncompatibleGame("=0.103.3"));
        Assert.Single(issues);
        Assert.Equal("LustTravel2", issues[0].ModId);
    }

    [Fact]
    public void JoinDisplayNames_truncates_after_max() {
        var issues = new List<KitLibCompatIssue> {
            new("a", "Alpha", KitLibCompatFlags.GameVersionMismatch, IncompatibleGame("=1")),
            new("b", "Beta", KitLibCompatFlags.GameVersionMismatch, IncompatibleGame("=1")),
            new("c", "Gamma", KitLibCompatFlags.GameVersionMismatch, IncompatibleGame("=1")),
            new("d", "Delta", KitLibCompatFlags.GameVersionMismatch, IncompatibleGame("=1")),
        };
        Assert.Equal("Alpha, Beta, Gamma…", KitLibCompatStartupScan.JoinDisplayNames(issues, ", ", 3));
    }
}
