namespace KitLib.Logging;

/// <summary>
/// Content-mod logging facade (NuGet compile-time). KitLib Core binds at bootstrap; no-op when unbound.
/// Mod id is resolved from the calling assembly; optional scope yields <c>[mod][scope]</c> lines.
/// </summary>
public static class KitLibLog {
    static Action<KitLogLevel, string?, string>? _writer;

    public static bool IsAvailable => _writer != null;

    /// <summary>Called by KitLib Core during bootstrap. Do not call from content mods.</summary>
    public static void Bind(Action<KitLogLevel, string?, string>? writer) => _writer = writer;

    public static void Debug(string message) => Write(KitLogLevel.Debug, null, message);
    public static void Info(string message) => Write(KitLogLevel.Info, null, message);
    public static void Warn(string message) => Write(KitLogLevel.Warn, null, message);
    public static void Error(string message) => Write(KitLogLevel.Error, null, message);

    public static void Debug(string scope, string message) => Write(KitLogLevel.Debug, scope, message);
    public static void Info(string scope, string message) => Write(KitLogLevel.Info, scope, message);
    public static void Warn(string scope, string message) => Write(KitLogLevel.Warn, scope, message);
    public static void Error(string scope, string message) => Write(KitLogLevel.Error, scope, message);

    public static KitLibLogScope Scope(string scope) => new(scope);

    public static void Write(KitLogLevel level, string? scope, string message)
        => _writer?.Invoke(level, scope, message);
}
