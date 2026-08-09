using MegaCrit.Sts2.Core.Logging;

namespace KitLib;

/// <summary>Level tokens for game log lines and structured pipe frames.</summary>
internal static class GameLogLineFormat {
    internal static string LevelToken(LogLevel level) => level switch {
        LogLevel.Error => "ERROR",
        LogLevel.Warn => "WARN",
        LogLevel.Debug => "DEBUG",
        LogLevel.VeryDebug => "VDB",
        LogLevel.Load => "LOAD",
        _ => "INFO",
    };
}
