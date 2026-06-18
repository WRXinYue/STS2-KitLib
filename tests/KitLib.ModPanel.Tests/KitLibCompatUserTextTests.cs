using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class KitLibCompatUserTextTests {
    [Theory]
    [InlineData("=0.103.3", "0.103.3")]
    [InlineData("^0.13.0", "0.13.0+")]
    [InlineData(">=0.106.0 <0.107.0", "0.106.0+")]
    [InlineData("v0.107.0", "0.107.0")]
    public void HumanizeVersionRange_strips_semver_operators(string raw, string expected) {
        Assert.Equal(expected, KitLibCompatUserText.HumanizeVersionRange(raw));
    }

    [Fact]
    public void JoinHumanizedRanges_uses_or_separator() {
        Assert.Equal(
            "0.103.3 or 0.106.0+",
            KitLibCompatUserText.JoinHumanizedRanges(["=0.103.3", ">=0.106.0"], " or "));
    }
}
