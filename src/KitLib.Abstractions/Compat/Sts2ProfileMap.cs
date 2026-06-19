using Semver;

namespace KitLib.Abstractions.Compat;

public static class Sts2ProfileMap {
    private static readonly Sts2Platform[] PcPlatforms = [
        Sts2Platform.Windows,
        Sts2Platform.macOS,
        Sts2Platform.Linux,
    ];

    public const string PinnedGameVersion = "0.107.1";

    public static readonly string[] PinnedGameVersions = [PinnedGameVersion];

    internal static Sts2GameProfile Resolve(SemVersion? version, Sts2Platform platform) {
        if (version == null || platform == Sts2Platform.Unknown)
            return Sts2GameProfile.Unknown;
        if (!Array.Exists(PcPlatforms, p => p == platform))
            return Sts2GameProfile.Unknown;
        if (version.Major == 0 && version.Minor >= 106)
            return Sts2GameProfile.Supported;
        return Sts2GameProfile.Unknown;
    }

    public static Sts2GameProfile Resolve(string? rawVersion, Sts2Platform platform) {
        if (!Sts2SemVersion.TryParse(rawVersion, out var version))
            return Sts2GameProfile.Unknown;
        return Resolve(version, platform);
    }
}
