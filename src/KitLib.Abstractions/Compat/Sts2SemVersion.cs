using Semver;

namespace KitLib.Abstractions.Compat;

public static class Sts2SemVersion {
    public static bool TryParse(string? raw, out SemVersion? version) {
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
}
