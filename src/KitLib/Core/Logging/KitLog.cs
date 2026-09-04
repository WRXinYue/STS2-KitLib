using MegaCrit.Sts2.Core.Logging;
using KitLogLevel = KitLib.Logging.KitLogLevel;

namespace KitLib;

/// <summary>KitLib internal logging via the official <see cref="Logger"/> (same as content mods).</summary>
public static class KitLog {
    public static void Debug(string message) => Write(null, message, KitLogLevel.Debug);
    public static void Info(string message) => Write(null, message, KitLogLevel.Info);
    public static void Warn(string message) => Write(null, message, KitLogLevel.Warn);
    public static void Error(string message) => Write(null, message, KitLogLevel.Error);

    public static void Debug(string scope, string message)
        => Write(KitLib.Logging.KitLibLogFormat.NormalizeKitLibScope(scope, MainFile.ModID), message, KitLogLevel.Debug);
    public static void Info(string scope, string message)
        => Write(KitLib.Logging.KitLibLogFormat.NormalizeKitLibScope(scope, MainFile.ModID), message, KitLogLevel.Info);
    public static void Warn(string scope, string message)
        => Write(KitLib.Logging.KitLibLogFormat.NormalizeKitLibScope(scope, MainFile.ModID), message, KitLogLevel.Warn);
    public static void Error(string scope, string message)
        => Write(KitLib.Logging.KitLibLogFormat.NormalizeKitLibScope(scope, MainFile.ModID), message, KitLogLevel.Error);

    internal static void Write(KitLogLevel level, string? scope, string message)
        => Write(scope, message, level);

    static void Write(string? scope, string message, KitLogLevel level) {
        var text = string.IsNullOrWhiteSpace(scope) ? message : $"[{scope.Trim()}] {message}";
        switch (level) {
            case KitLogLevel.Error:
                MainFile.Logger.Error(text);
                break;
            case KitLogLevel.Warn:
                MainFile.Logger.Warn(text);
                break;
            case KitLogLevel.Debug:
                MainFile.Logger.Debug(text);
                break;
            default:
                MainFile.Logger.Info(text);
                break;
        }
    }
}
