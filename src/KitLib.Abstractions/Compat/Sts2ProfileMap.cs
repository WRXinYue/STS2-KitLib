using Semver;

namespace KitLib.Abstractions.Compat;

public static class Sts2ProfileMap {
    private static readonly Sts2Platform[] PcPlatforms = [
        Sts2Platform.Windows,
        Sts2Platform.macOS,
        Sts2Platform.Linux,
    ];

    /// <summary>Exact game versions KitLib is built and tested against; add a row when STS2 ships a new API line.</summary>
    private static readonly (SemVersion Version, Sts2GameProfile Profile)[] PinnedVersions = [
        (new SemVersion(0, 103, 3), Sts2GameProfile.StablePre106),
        (new SemVersion(0, 107, 0), Sts2GameProfile.Beta106Plus),
    ];

    public static readonly string[] PinnedGameVersions = PinnedVersions
        .Select(p => $"0.{p.Version.Minor}.{p.Version.Patch}")
        .ToArray();

    internal static Sts2GameProfile Resolve(SemVersion? version, Sts2Platform platform) {
        if (version == null || platform == Sts2Platform.Unknown)
            return Sts2GameProfile.Unknown;
        if (!Array.Exists(PcPlatforms, p => p == platform))
            return Sts2GameProfile.Unknown;

        foreach (var (pinned, profile) in PinnedVersions) {
            if (SameReleaseVersion(version, pinned))
                return profile;
        }

        return Sts2GameProfile.Unknown;
    }

    public static Sts2GameProfile Resolve(string? rawVersion, Sts2Platform platform) {
        if (!Sts2SemVersion.TryParse(rawVersion, out var version))
            return Sts2GameProfile.Unknown;
        return Resolve(version, platform);
    }

    private static bool SameReleaseVersion(SemVersion runtime, SemVersion pinned) =>
        runtime.Major == pinned.Major
        && runtime.Minor == pinned.Minor
        && runtime.Patch == pinned.Patch;
}
