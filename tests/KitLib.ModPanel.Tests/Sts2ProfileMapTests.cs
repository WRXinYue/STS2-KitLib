using KitLib.Abstractions.Compat;

namespace KitLib.ModPanel.Tests;

public sealed class Sts2ProfileMapTests {
    [Fact]
    public void Resolve_stable_1033_windows() {
        Assert.Equal(
            Sts2GameProfile.StablePre106,
            Sts2ProfileMap.Resolve("0.103.3", Sts2Platform.Windows));
    }

    [Fact]
    public void Resolve_beta_1070_windows() {
        Assert.Equal(
            Sts2GameProfile.Beta106Plus,
            Sts2ProfileMap.Resolve("0.107.0", Sts2Platform.Windows));
    }

    [Fact]
    public void Resolve_v_prefix_beta() {
        Assert.Equal(
            Sts2GameProfile.Beta106Plus,
            Sts2ProfileMap.Resolve("v0.107.0", Sts2Platform.macOS));
    }

    [Fact]
    public void Resolve_unpinned_non_beta_versions_fall_back_to_stable() {
        Assert.Equal(
            Sts2GameProfile.StablePre106,
            Sts2ProfileMap.Resolve("0.103.2", Sts2Platform.Windows));
        Assert.Equal(
            Sts2GameProfile.StablePre106,
            Sts2ProfileMap.Resolve("0.105.9", Sts2Platform.Windows));
        Assert.Equal(
            Sts2GameProfile.StablePre106,
            Sts2ProfileMap.Resolve("0.106.1", Sts2Platform.Windows));
        Assert.Equal(
            Sts2GameProfile.StablePre106,
            Sts2ProfileMap.Resolve("0.108.0", Sts2Platform.Windows));
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
    public void Resolve_android_unknown_even_when_pinned() {
        Assert.Equal(
            Sts2GameProfile.Unknown,
            Sts2ProfileMap.Resolve("0.107.0", Sts2Platform.Android));
    }

    [Fact]
    public void PinnedGameVersions_lists_exact_pins() {
        Assert.Equal(["0.103.3", "0.107.0"], Sts2ProfileMap.PinnedGameVersions);
    }
}
