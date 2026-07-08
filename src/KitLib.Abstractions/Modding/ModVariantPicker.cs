using KitLib.Abstractions.Compat;

namespace KitLib.Abstractions.Modding;

/// <summary>Picks a bundled compat target for the running STS2 host version.</summary>
public static class ModVariantPicker {
    public static string? PickCompatTarget(IReadOnlyList<string> bundledTargets, Version? hostVersion) {
        if (bundledTargets.Count == 0)
            return null;

        var parsed = new List<(string Label, Version Version)>(bundledTargets.Count);
        foreach (var label in bundledTargets) {
            if (string.IsNullOrWhiteSpace(label))
                continue;
            if (!Sts2GameVersion.TryParseCore(label, out var version))
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
