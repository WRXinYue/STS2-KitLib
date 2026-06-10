using Semver;

namespace KitLib.Abstractions.Compat;

public static class Sts2ProfileMap {
    private static readonly (Sts2Platform[] Platforms, string Range, Sts2GameProfile Profile)[] Rules = [
        ([Sts2Platform.Windows, Sts2Platform.macOS, Sts2Platform.Linux], ">=0.105.0 <0.106.0", Sts2GameProfile.StablePre106),
        ([Sts2Platform.Windows, Sts2Platform.macOS, Sts2Platform.Linux], ">=0.106.0 <0.108.0", Sts2GameProfile.Beta106Plus),
    ];

    internal static Sts2GameProfile Resolve(SemVersion? version, Sts2Platform platform) {
        if (version == null || platform == Sts2Platform.Unknown)
            return Sts2GameProfile.Unknown;

        foreach (var (platforms, rangeText, profile) in Rules) {
            if (!Array.Exists(platforms, p => p == platform))
                continue;
            if (!SemVersionRange.TryParseNpm(rangeText, includeAllPrerelease: false, out var range))
                continue;
            if (version.Satisfies(range))
                return profile;
        }

        return Sts2GameProfile.Unknown;
    }

    public static Sts2GameProfile Resolve(string? rawVersion, Sts2Platform platform) {
        if (!Sts2SemVersion.TryParse(rawVersion, out var version))
            return Sts2GameProfile.Unknown;
        return Resolve(version, platform);
    }
}
