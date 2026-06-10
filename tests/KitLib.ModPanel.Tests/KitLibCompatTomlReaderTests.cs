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
}
