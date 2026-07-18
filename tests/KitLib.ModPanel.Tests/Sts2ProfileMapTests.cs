using KitLib.Abstractions.Compat;

namespace KitLib.ModPanel.Tests;

public sealed class Sts2ProfileMapTests {
    [Fact]
    public void Resolve_pinned_1090_windows() {
        Assert.Equal(
            Sts2GameProfile.Supported,
            Sts2ProfileMap.Resolve("0.109.0", Sts2Platform.Windows));
    }

    [Fact]
    public void Resolve_v_prefix() {
        Assert.Equal(
            Sts2GameProfile.Supported,
            Sts2ProfileMap.Resolve("v0.109.0", Sts2Platform.macOS));
    }

    [Fact]
    public void Resolve_unpinned_106plus_supported() {
        Assert.Equal(
            Sts2GameProfile.Supported,
            Sts2ProfileMap.Resolve("0.106.1", Sts2Platform.Windows));
        Assert.Equal(
            Sts2GameProfile.Supported,
            Sts2ProfileMap.Resolve("0.108.0", Sts2Platform.Windows));
    }

    [Fact]
    public void Resolve_pre106_unknown() {
        Assert.Equal(
            Sts2GameProfile.Unknown,
            Sts2ProfileMap.Resolve("0.103.3", Sts2Platform.Windows));
        Assert.Equal(
            Sts2GameProfile.Unknown,
            Sts2ProfileMap.Resolve("0.105.9", Sts2Platform.Windows));
    }

    [Fact]
    public void Resolve_empty_version_unknown() {
        Assert.Equal(
            Sts2GameProfile.Unknown,
            Sts2ProfileMap.Resolve((string?)null, Sts2Platform.Windows));
        Assert.Equal(
            Sts2GameProfile.Unknown,
            Sts2ProfileMap.Resolve("", Sts2Platform.Linux));
    }

    [Fact]
    public void Resolve_android_unknown_even_when_supported_version() {
        Assert.Equal(
            Sts2GameProfile.Unknown,
            Sts2ProfileMap.Resolve("0.109.0", Sts2Platform.Android));
    }

    [Fact]
    public void PinnedGameVersions_lists_exact_pins() {
        Assert.Equal([Sts2ProfileMap.PinnedGameVersion], Sts2ProfileMap.PinnedGameVersions);
    }
}
