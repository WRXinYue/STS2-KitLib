namespace KitLib.Logging;

/// <summary>Line format for content-mod logging: <c>[mod]</c> or <c>[mod][scope]</c>.</summary>
public static class KitLibLogFormat {
    /// <summary>
    /// Builds a log line prefix. First bracket segment is the manifest mod id; optional second is a sub-module scope.
    /// </summary>
    public static string FormatLine(string modId, string? scope, string? message) {
        if (string.IsNullOrWhiteSpace(modId))
            modId = "Unknown";

        var prefix = string.IsNullOrWhiteSpace(scope)
            ? $"[{modId}]"
            : $"[{modId}][{scope}]";

        return string.IsNullOrEmpty(message) ? prefix : $"{prefix} {message}";
    }

    /// <summary>
    /// Source token for sinks that wrap as <c>[{source}] {message}</c> to produce <c>[mod][scope] message</c>.
    /// </summary>
    public static string FormatCompoundSource(string modId, string? scope)
        => string.IsNullOrWhiteSpace(scope) ? modId : $"{modId}][{scope}";

    /// <summary>Maps legacy single-bracket tags to a KitLib sub-module scope.</summary>
    public static string? NormalizeKitLibScope(string? scope, string modId = "KitLib") {
        if (string.IsNullOrWhiteSpace(scope))
            return null;

        scope = scope.Trim();
        if (scope.Equals(modId, StringComparison.OrdinalIgnoreCase))
            return null;
        if (scope.Equals("KitLibHost", StringComparison.OrdinalIgnoreCase))
            return "Host";
        if (scope.StartsWith("KitLib.", StringComparison.OrdinalIgnoreCase))
            return scope["KitLib.".Length..];
        if (scope.StartsWith("KitLib ", StringComparison.OrdinalIgnoreCase))
            return scope["KitLib ".Length..].Trim();
        return scope;
    }
}
