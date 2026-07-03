using System.Text.Json;
using KitLib.Abstractions.Compat;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.ModVariantLoader;

internal static class Sts2HostVersion {
    private static readonly Lazy<HostVersionSnapshot> Lazy = new(Resolve);

    internal static Version? Numeric => Lazy.Value.Numeric;

    internal static string? ReleaseLabel => Lazy.Value.ReleaseLabel;

    private static HostVersionSnapshot Resolve() {
        string? fallbackLabel = null;

        try {
            var ri = ReleaseInfoManager.Instance.ReleaseInfo;
            if (TryCaptureVersionLabel(ri?.Version, ref fallbackLabel, out var snapshot))
                return snapshot;
        }
        catch {
            // ReleaseInfoManager may be unavailable in unusual environments.
        }

        var av = typeof(SerializableRun).Assembly.GetName().Version;
        if (av != null && !IsAllZero(av))
            return new(av, fallbackLabel);

        return new(null, fallbackLabel);
    }

    private static bool IsAllZero(Version v) =>
        v.Major == 0 && v is { Minor: 0, Build: 0, Revision: 0 };

    private static bool TryCaptureVersionLabel(
        string? label,
        ref string? fallbackLabel,
        out HostVersionSnapshot snapshot) {
        snapshot = default;
        if (string.IsNullOrWhiteSpace(label))
            return false;

        fallbackLabel ??= label;
        if (!Sts2GameVersion.TryParseCore(label, out var v))
            return false;

        snapshot = new(v, label);
        return true;
    }

    private readonly record struct HostVersionSnapshot(Version? Numeric, string? ReleaseLabel);
}
