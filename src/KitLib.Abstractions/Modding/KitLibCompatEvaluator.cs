using Semver;

namespace KitLib.Abstractions.Modding;

public static class KitLibCompatEvaluator {
    static readonly Dictionary<string, string[]> TransitiveModules =
        new(StringComparer.OrdinalIgnoreCase) {
            ["KitLib.Dev"] = ["KitLib.Panel"],
            ["KitLib.Cheat"] = ["KitLib.Panel"],
        };

    public static KitLibCompatResult Evaluate(KitLibCompatDocument? document, KitLibCompatRuntime runtime) {
        if (document == null || !document.HasConstraints) {
            return new KitLibCompatResult {
                HasSidecar = document != null,
                Flags = KitLibCompatFlags.None,
            };
        }

        var flags = KitLibCompatFlags.None;
        var missing = new List<string>();

        if (document.GameVersionRanges.Count > 0
            && !AnyRangeSatisfied(document.GameVersionRanges, runtime.GameVersion)) {
            flags |= KitLibCompatFlags.GameVersionMismatch;
        }

        if (document.KitLibVersionRanges.Count > 0
            && !AnyRangeSatisfied(document.KitLibVersionRanges, runtime.KitLibVersion)) {
            flags |= KitLibCompatFlags.KitLibVersionMismatch;
        }

        if (document.KitLibModules.Count > 0) {
            var required = ExpandModules(document.KitLibModules);
            var isLoaded = runtime.IsModuleLoaded ?? (_ => false);
            foreach (var moduleId in required) {
                if (!isLoaded(moduleId))
                    missing.Add(moduleId);
            }

            if (missing.Count > 0)
                flags |= KitLibCompatFlags.MissingKitLibModule;
        }

        var modMismatches = new List<string>();
        if (document.ModVersionRanges.Count > 0) {
            var resolve = runtime.ResolveModVersion ?? (_ => null);
            foreach (var (modId, ranges) in document.ModVersionRanges) {
                if (ranges.Count == 0)
                    continue;
                var loadedVersion = resolve(modId);
                if (AnyRangeSatisfied(ranges, loadedVersion))
                    continue;
                modMismatches.Add(FormatModDependencyMismatch(modId, ranges, loadedVersion));
            }

            if (modMismatches.Count > 0)
                flags |= KitLibCompatFlags.ModDependencyVersionMismatch;
        }

        return new KitLibCompatResult {
            HasSidecar = true,
            Flags = flags,
            GameVersionRanges = document.GameVersionRanges,
            KitLibVersionRanges = document.KitLibVersionRanges,
            MissingModules = missing,
            ModDependencyMismatches = modMismatches,
        };
    }

    internal static bool AnyRangeSatisfied(IReadOnlyList<string> ranges, string? rawVersion) {
        if (ranges.Count == 0)
            return true;
        if (!TryParseVersion(rawVersion, out var version))
            return false;
        foreach (var rangeText in ranges) {
            if (string.IsNullOrWhiteSpace(rangeText))
                continue;
            if (!TryParseRange(rangeText, out var range) || range == null)
                continue;
            if (version!.Satisfies(range))
                return true;
        }
        return false;
    }

    internal static bool TryParseVersion(string? raw, out SemVersion? version) {
        version = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        var normalized = raw.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            normalized = normalized[1..].TrimStart();
        if (!SemVersion.TryParse(normalized, SemVersionStyles.Any, out var parsed))
            return false;
        version = parsed;
        return true;
    }

    internal static bool TryParseRange(string raw, out SemVersionRange? range) {
        range = null;
        var text = raw.Trim();
        if (text.Length == 0)
            return false;
        if (!SemVersionRange.TryParseNpm(text, includeAllPrerelease: false, out var parsed))
            return false;
        range = parsed;
        return true;
    }

    static IReadOnlyList<string> ExpandModules(IReadOnlyList<string> modules) {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        foreach (var moduleId in modules) {
            if (string.IsNullOrWhiteSpace(moduleId))
                continue;
            AddModule(moduleId.Trim(), seen, ordered);
            if (!TransitiveModules.TryGetValue(moduleId.Trim(), out var transitive))
                continue;
            foreach (var dep in transitive)
                AddModule(dep, seen, ordered);
        }
        return ordered;
    }

    static void AddModule(string moduleId, HashSet<string> seen, List<string> ordered) {
        if (!seen.Add(moduleId))
            return;
        ordered.Add(moduleId);
    }

    static string FormatModDependencyMismatch(string modId, IReadOnlyList<string> ranges, string? loadedVersion) {
        var required = string.Join(" or ", ranges);
        var found = string.IsNullOrWhiteSpace(loadedVersion) ? "not loaded" : loadedVersion.Trim();
        return $"{modId} requires {required} (found {found})";
    }
}
