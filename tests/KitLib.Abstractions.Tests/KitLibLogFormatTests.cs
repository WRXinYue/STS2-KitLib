using KitLib.Logging;

namespace KitLib.Abstractions.Tests;

[Collection("KitLibLog")]
public class KitLibLogFormatTests {
    [Fact]
    public void FormatGameLoggerText_hostOmitsRepeatedModId() {
        Assert.Equal("loaded", KitLibLogFormat.FormatGameLoggerText("KitLib", null, "loaded"));
        Assert.Equal("[ProgressGuard] starting", KitLibLogFormat.FormatGameLoggerText("KitLib", "ProgressGuard", "starting"));
    }

    [Fact]
    public void FormatGameLoggerText_contentModKeepsFullPrefix() {
        Assert.Equal("[my-mod][Combat] turn 3", KitLibLogFormat.FormatGameLoggerText("my-mod", "Combat", "turn 3"));
    }

    [Fact]
    public void FormatGameCallbackText_hostScoped() {
        Assert.Equal("[KitLib] [PseudoCoop] starting", KitLibLogFormat.FormatGameCallbackText("KitLib", "PseudoCoop", "starting"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("KitLib", null)]
    [InlineData("kitlib", null)]
    [InlineData("KitLibHost", "Host")]
    [InlineData("KitLib.CombatAdd", "CombatAdd")]
    [InlineData("KitLib CrashRecovery", "CrashRecovery")]
    [InlineData("MpCheat", "MpCheat")]
    [InlineData("  AiHost  ", "AiHost")]
    public void NormalizeKitLibScope_maps_legacy_tags(string? input, string? expected) {
        Assert.Equal(expected, KitLibLogFormat.NormalizeKitLibScope(input));
    }

    [Fact]
    public void NormalizeKitLibScope_respects_custom_mod_id() {
        Assert.Null(KitLibLogFormat.NormalizeKitLibScope("my-mod", "my-mod"));
        Assert.Equal("Combat", KitLibLogFormat.NormalizeKitLibScope("Combat", "my-mod"));
    }
}
