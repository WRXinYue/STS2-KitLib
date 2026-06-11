using System.IO;

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
    /// Text for the host mod game logger; the engine prepends <c>[hostModId]</c> in callbacks.
    /// Host-internal lines omit the repeated mod id and use <c>[scope]</c> when scoped.
    /// </summary>
    public static string FormatGameLoggerText(string modId, string? scope, string? message, string hostModId = "KitLib") {
        if (string.IsNullOrWhiteSpace(modId))
            modId = "Unknown";

        if (!string.Equals(modId, hostModId, StringComparison.OrdinalIgnoreCase))
            return FormatLine(modId, scope, message);

        var prefix = string.IsNullOrWhiteSpace(scope) ? "" : $"[{scope.Trim()}]";
        if (string.IsNullOrEmpty(message))
            return prefix;

        return string.IsNullOrEmpty(prefix) ? message : $"{prefix} {message}";
    }

    /// <summary>Full text expected from <c>Log.LogCallback</c> after the engine prepends <c>[hostModId]</c>.</summary>
    public static string FormatGameCallbackText(string modId, string? scope, string? message, string hostModId = "KitLib") {
        var body = FormatGameLoggerText(modId, scope, message, hostModId);
        return string.IsNullOrEmpty(body) ? $"[{hostModId}]" : $"[{hostModId}] {body}";
    }

    /// <summary>Prefixes a message with caller location: <c>File.cs:42 MethodName | message</c>.</summary>
    public static string FormatWithCaller(string message, string member, string file, int line) {
        var fileName = string.IsNullOrEmpty(file) ? "?" : Path.GetFileName(file);
        var memberName = string.IsNullOrEmpty(member) ? "?" : member;
        return $"{fileName}:{line} {memberName} | {message}";
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
