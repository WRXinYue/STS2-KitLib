namespace KitLib.Logging;

/// <summary>KitLib-internal line formatting; host scoped lines match official <c>Logger.Info("[Scope] …")</c>.</summary>
internal static class KitLibLogFormat {
    static string FormatLine(string modId, string? scope, string? message) {
        if (string.IsNullOrWhiteSpace(modId))
            modId = "Unknown";

        var prefix = string.IsNullOrWhiteSpace(scope)
            ? $"[{modId}]"
            : $"[{modId}][{scope}]";

        return string.IsNullOrEmpty(message) ? prefix : $"{prefix} {message}";
    }

    /// <summary>
    /// Text passed to the host mod <see cref="MegaCrit.Sts2.Core.Logging.Logger"/>.
    /// Scoped lines use the same in-message tag as content mods, e.g. <c>[ProgressGuard] …</c>;
    /// the engine prefixes <c>[KitLib] </c> (see LustTravel2 <c>[Bootstrap]</c>).
    /// </summary>
    internal static string FormatGameLoggerText(string modId, string? scope, string? message, string hostModId = "KitLib") {
        if (string.IsNullOrWhiteSpace(modId))
            modId = "Unknown";

        if (!string.Equals(modId, hostModId, StringComparison.OrdinalIgnoreCase))
            return FormatLine(modId, scope, message);

        if (string.IsNullOrWhiteSpace(scope))
            return message ?? "";

        var scopeTag = $"[{scope.Trim()}]";
        return string.IsNullOrEmpty(message) ? scopeTag : $"{scopeTag} {message}";
    }

    /// <summary>Full text from <c>Log.LogCallback</c> / godot.log after the engine prepends <c>[hostModId] </c>.</summary>
    internal static string FormatGameCallbackText(string modId, string? scope, string? message, string hostModId = "KitLib") {
        if (!string.Equals(modId, hostModId, StringComparison.OrdinalIgnoreCase))
            return FormatLine(modId, scope, message);

        if (string.IsNullOrWhiteSpace(scope))
            return string.IsNullOrEmpty(message) ? $"[{hostModId}]" : $"[{hostModId}] {message}";

        var scopeTag = $"[{scope.Trim()}]";
        if (string.IsNullOrEmpty(message))
            return $"[{hostModId}] {scopeTag}";

        return $"[{hostModId}] {scopeTag} {message}";
    }

    /// <summary>Maps legacy single-bracket tags to a KitLib sub-module scope.</summary>
    internal static string? NormalizeKitLibScope(string? scope, string modId = "KitLib") {
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
