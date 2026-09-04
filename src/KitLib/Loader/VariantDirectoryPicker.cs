namespace KitLib;

internal static class VariantDirectoryPicker {
    internal const string LibDirectoryName = "lib";
    internal const string CompatTargetMarkerName = "compat-target.txt";
    internal const string CoreFileName = "KitLib.Core.dll";

    internal static string? TryPick(string modDir, Version? hostVersion, string requiredFileName = CoreFileName) {
        var libRoot = Path.Combine(modDir, LibDirectoryName);
        if (!Directory.Exists(libRoot))
            return null;

        var bundled = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(libRoot)) {
            var marker = Path.Combine(dir, CompatTargetMarkerName);
            if (!File.Exists(marker))
                continue;

            var label = File.ReadAllText(marker).Trim();
            if (string.IsNullOrWhiteSpace(label))
                continue;

            if (!string.IsNullOrEmpty(requiredFileName) &&
                !File.Exists(Path.Combine(dir, requiredFileName)))
                continue;

            bundled.Add(label);
        }

        if (bundled.Count == 0)
            return null;

        var picked = PickCompatTarget(bundled, hostVersion);
        return picked is null ? null : Path.Combine(libRoot, picked);
    }

    internal static string? PickCompatTarget(IReadOnlyList<string> bundledTargets, Version? hostVersion) {
        if (bundledTargets.Count == 0)
            return null;

        var parsed = new List<(string Label, Version Version)>(bundledTargets.Count);
        foreach (var label in bundledTargets) {
            if (string.IsNullOrWhiteSpace(label))
                continue;
            if (!HostVersionProbe.TryParseCore(label, out var version))
                continue;
            parsed.Add((label.Trim(), version));
        }

        if (parsed.Count == 0)
            return null;

        parsed.Sort(static (a, b) => a.Version.CompareTo(b.Version));

        if (hostVersion is null)
            return parsed[^1].Label;

        var candidates = parsed.Where(x => x.Version <= hostVersion).ToList();
        return candidates.Count > 0 ? candidates[^1].Label : parsed[^1].Label;
    }
}
