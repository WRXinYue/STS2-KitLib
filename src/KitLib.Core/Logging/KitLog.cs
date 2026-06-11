using MegaCrit.Sts2.Core.Logging;

namespace KitLib;

/// <summary>Log levels aligned with <c>KitLib.Abstractions.Logging.KitLogLevel</c> ordinals.</summary>
public enum KitLogLevel {
    Debug,
    Info,
    Warn,
    Error,
}

/// <summary>Unified logging for KitLib: internal lines use <c>[KitLib]</c> or <c>[KitLib][scope]</c>.</summary>
public static class KitLog {
    public static void Debug(string message) => WriteMod(KitLogLevel.Debug, MainFile.ModID, null, message);
    public static void Info(string message) => WriteMod(KitLogLevel.Info, MainFile.ModID, null, message);
    public static void Warn(string message) => WriteMod(KitLogLevel.Warn, MainFile.ModID, null, message);
    public static void Error(string message) => WriteMod(KitLogLevel.Error, MainFile.ModID, null, message);

    public static void Debug(string scope, string message)
        => WriteMod(KitLogLevel.Debug, MainFile.ModID, KitLib.Logging.KitLibLogFormat.NormalizeKitLibScope(scope, MainFile.ModID), message);
    public static void Info(string scope, string message)
        => WriteMod(KitLogLevel.Info, MainFile.ModID, KitLib.Logging.KitLibLogFormat.NormalizeKitLibScope(scope, MainFile.ModID), message);
    public static void Warn(string scope, string message)
        => WriteMod(KitLogLevel.Warn, MainFile.ModID, KitLib.Logging.KitLibLogFormat.NormalizeKitLibScope(scope, MainFile.ModID), message);
    public static void Error(string scope, string message)
        => WriteMod(KitLogLevel.Error, MainFile.ModID, KitLib.Logging.KitLibLogFormat.NormalizeKitLibScope(scope, MainFile.ModID), message);

    /// <summary>Content-mod or dynamic scope; same as <see cref="WriteMod"/>.</summary>
    public static void Write(KitLogLevel level, string? scope, string message)
        => WriteMod(level, MainFile.ModID, KitLib.Logging.KitLibLogFormat.NormalizeKitLibScope(scope, MainFile.ModID), message);

    /// <summary>Content-mod line format: <c>[modId]</c> or <c>[modId][scope]</c>.</summary>
    public static void WriteMod(KitLogLevel level, string modId, string? scope, string message) {
        var absLevel = MapAbsLevel(level);
        var entry = KitLib.Logging.LogStreamEntry.FromKitLog(absLevel, modId, scope, message, MainFile.ModID);
        KitLib.Logging.LogStreamHub.Publish(entry);
        KitLib.Logging.StructuredLogDedupe.Mark(entry.Fingerprint);

        var line = KitLib.Logging.KitLibLogFormat.FormatGameLoggerText(modId, scope, message, MainFile.ModID);
        WriteRaw(level, line);
    }

    static KitLib.Logging.KitLogLevel MapAbsLevel(KitLogLevel level) => level switch {
        KitLogLevel.Error => KitLib.Logging.KitLogLevel.Error,
        KitLogLevel.Warn => KitLib.Logging.KitLogLevel.Warn,
        KitLogLevel.Debug => KitLib.Logging.KitLogLevel.Debug,
        _ => KitLib.Logging.KitLogLevel.Info,
    };

    static void WriteRaw(KitLogLevel level, string text) {
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
