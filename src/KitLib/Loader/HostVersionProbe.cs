using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib;

/// <summary>Host STS2 version without referencing KitLib.Abstractions.</summary>
internal static class HostVersionProbe {
    static readonly Lazy<Snapshot> Lazy = new(Resolve);

    internal static Version? Numeric => Lazy.Value.Numeric;

    internal static string? ReleaseLabel => Lazy.Value.ReleaseLabel;

    static Snapshot Resolve() {
        string? fallbackLabel = null;

        try {
            var ri = ReleaseInfoManager.Instance.ReleaseInfo;
            if (TryCapture(ri?.Version, ref fallbackLabel, out var snapshot))
                return snapshot;
        }
        catch {
            // ReleaseInfoManager may be unavailable in unusual environments.
        }

        var av = typeof(SerializableRun).Assembly.GetName().Version;
        if (av != null && !IsAllZero(av))
            return new Snapshot(av, fallbackLabel);

        return new Snapshot(null, fallbackLabel);
    }

    static bool IsAllZero(Version v) =>
        v.Major == 0 && v is { Minor: 0, Build: 0, Revision: 0 };

    static bool TryCapture(string? label, ref string? fallbackLabel, out Snapshot snapshot) {
        snapshot = default;
        if (string.IsNullOrWhiteSpace(label))
            return false;

        fallbackLabel ??= label;
        if (!TryParseCore(label, out var v))
            return false;

        snapshot = new Snapshot(v, label);
        return true;
    }

    internal static bool TryParseCore(string text, out Version version) {
        var s = text.Trim();
        var dash = s.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
            s = s[..dash].Trim();
        var plus = s.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
            s = s[..plus].Trim();
        if (s.Length >= 2 && (s[0] == 'v' || s[0] == 'V') && char.IsDigit(s[1]))
            s = s[1..];
        if (Version.TryParse(s, out var parsed)) {
            version = parsed;
            return true;
        }

        version = new Version(0, 0);
        return false;
    }

    readonly record struct Snapshot(Version? Numeric, string? ReleaseLabel);
}
