using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class KitLibCompatTomlReaderTests {
    [Fact]
    public void TryParse_reads_game_and_kitlib_sections() {
        const string toml = """
            [game]
            version = [">=0.106.0 <0.107.0", "^0.105.0"]

            [kitlib]
            version = ">=0.13.0"
            modules = ["KitLib", "KitLib.Dev"]
            """;
        Assert.True(KitLibCompatTomlReader.TryParse(toml, out var doc));
        Assert.NotNull(doc);
        Assert.Equal(2, doc!.GameVersionRanges.Count);
        Assert.Single(doc.KitLibVersionRanges);
        Assert.Equal(2, doc.KitLibModules.Count);
        Assert.Equal("KitLib.Dev", doc.KitLibModules[1]);
    }

    [Fact]
    public void TryParse_supports_scalar_game_version() {
        const string toml = """
            [game]
            version = "^0.106.0"
            """;
        Assert.True(KitLibCompatTomlReader.TryParse(toml, out var doc));
        Assert.NotNull(doc);
        Assert.Single(doc!.GameVersionRanges);
    }

    [Fact]
    public void TryParse_reads_dependencies_section() {
        const string toml = """
            [dependencies]
            "STS2-RitsuLib" = ">=0.4.10"
            KitLib = [">=0.13.0", "<0.14.0"]
            """;
        Assert.True(KitLibCompatTomlReader.TryParse(toml, out var doc));
        Assert.NotNull(doc);
        Assert.Equal(2, doc!.ModVersionRanges.Count);
        Assert.True(doc.ModVersionRanges.TryGetValue("STS2-RitsuLib", out var ritsu));
        Assert.Single(ritsu!);
        Assert.Equal(">=0.4.10", ritsu[0]);
        Assert.True(doc.ModVersionRanges.TryGetValue("KitLib", out var kitLib));
        Assert.Equal(2, kitLib!.Count);
    }

    [Fact]
    public void TryParse_reads_lust_travel2_compat_sidecar() {
        const string toml = """
            [game]
            version = "=0.103.3"

            [kitlib]
            version = "^0.13.0"

            [dependencies]
            "STS2-RitsuLib" = ">=0.4.15"
            """;
        Assert.True(KitLibCompatTomlReader.TryParse(toml, out var doc));
        Assert.NotNull(doc);
        Assert.Equal("=0.103.3", doc!.GameVersionRanges[0]);
        Assert.Equal("^0.13.0", doc.KitLibVersionRanges[0]);
        var runtime = new KitLibCompatRuntime { GameVersion = "0.107.0", KitLibVersion = "0.13.5" };
        Assert.False(KitLibCompatEvaluator.Evaluate(doc, runtime).IsCompatible);
    }
}
