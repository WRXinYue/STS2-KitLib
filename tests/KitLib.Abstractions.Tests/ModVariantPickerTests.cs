using KitLib.Abstractions.Modding;

namespace KitLib.Abstractions.Tests;

public sealed class ModVariantPickerTests {
    [Fact]
    public void PickCompatTarget_selects_newest_not_above_host() {
        var picked = ModVariantPicker.PickCompatTarget(
            ["0.107.1", "0.108.0"],
            new Version(0, 107, 1));
        Assert.Equal("0.107.1", picked);
    }

    [Fact]
    public void PickCompatTarget_selects_beta_on_108_host() {
        var picked = ModVariantPicker.PickCompatTarget(
            ["0.107.1", "0.108.0"],
            new Version(0, 108, 0));
        Assert.Equal("0.108.0", picked);
    }

    [Fact]
    public void PickCompatTarget_falls_back_to_newest_when_host_is_newer() {
        var picked = ModVariantPicker.PickCompatTarget(
            ["0.107.1", "0.108.0"],
            new Version(0, 109, 0));
        Assert.Equal("0.108.0", picked);
    }
}
