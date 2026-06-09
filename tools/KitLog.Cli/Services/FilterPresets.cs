namespace KitLog.Cli.Services;

internal static class FilterPresets {
    public const string AiPattern = @"\[(AutoPlay|AiHost|MpAi|LanLocal|Companion)\]";

    public static string? Resolve(string? filter) {
        if (string.IsNullOrWhiteSpace(filter))
            return null;

        return filter.Trim().ToLowerInvariant() switch {
            "ai" => AiPattern,
            _ => filter,
        };
    }
}
