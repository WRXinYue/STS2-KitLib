using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class KitLibCompatDisplayTests {
    [Fact]
    public void AppendBadge_adds_suffix_once() {
        Assert.Equal("LustTravel2 (KitLib)", KitLibCompatDisplay.AppendBadge("LustTravel2"));
        Assert.Equal("LustTravel2 (KitLib)", KitLibCompatDisplay.AppendBadge("LustTravel2 (KitLib)"));
    }

    [Fact]
    public void FormatSidebarDisplayName_skips_without_sidecar() {
        Assert.Equal("LustTravel2", KitLibCompatDisplay.FormatSidebarDisplayName("LustTravel2", null));
    }
}
