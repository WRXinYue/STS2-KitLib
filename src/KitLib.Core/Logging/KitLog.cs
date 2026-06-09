using MegaCrit.Sts2.Core.Logging;

namespace KitLib;

/// <summary>Log levels aligned with <c>KitLib.Abstractions.Logging.KitLogLevel</c> ordinals.</summary>
public enum KitLogLevel {
    Debug,
    Info,
    Warn,
    Error,
}

/// <summary>Unified logging entry for KitLib modules and content mods.</summary>
public static class KitLog {
    public static void Debug(string source, string message) => Write(KitLogLevel.Debug, source, message);
    public static void Info(string source, string message) => Write(KitLogLevel.Info, source, message);
    public static void Warn(string source, string message) => Write(KitLogLevel.Warn, source, message);
    public static void Error(string source, string message) => Write(KitLogLevel.Error, source, message);

    public static void Write(KitLogLevel level, string source, string message) {
        if (string.IsNullOrEmpty(source))
            source = MainFile.ModID;

        var text = string.IsNullOrEmpty(message)
            ? $"[{source}]"
            : $"[{source}] {message}";

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

        KitLogHub.Publish(level, source, message);
    }
}
