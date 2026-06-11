using MegaCrit.Sts2.Core.Logging;

namespace KitLib;

/// <summary>Disk format for <c>instances/{pid}/session.log</c>; shared by InstanceLogWriter and kitlog tail.</summary>
internal static class SessionLogLineFormat {
    public static string Format(LogLevel level, string text) {
        var time = DateTime.Now.ToString("HH:mm:ss");
        return $"{time} {LevelToken(level)} {text}";
    }

    internal static string LevelToken(LogLevel level) => level switch {
        LogLevel.Error => "ERROR",
        LogLevel.Warn => "WARN",
        LogLevel.Debug => "DEBUG",
        LogLevel.VeryDebug => "VDB",
        LogLevel.Load => "LOAD",
        _ => "INFO",
    };
}
