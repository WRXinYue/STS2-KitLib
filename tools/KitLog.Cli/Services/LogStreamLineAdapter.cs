using KitLib.Logging;
using KitLog.Cli.Rendering;

namespace KitLog.Cli.Services;

internal static class LogStreamLineAdapter {
    public static ParsedLogLine ToParsedLine(LogStreamEntry entry) {
        var level = ParseLevel(entry.Lvl);
        var display = LogStreamLineRenderer.FormatPlain(entry);
        return new ParsedLogLine(level, display, display, HasWallClockTime: true);
    }

    public static ParsedLogLevel ParseLevel(string lvl) => lvl.ToLowerInvariant() switch {
        "error" => ParsedLogLevel.Error,
        "warn" or "warning" => ParsedLogLevel.Warn,
        "debug" or "dbg" => ParsedLogLevel.Debug,
        "vdb" or "verydebug" => ParsedLogLevel.VeryDebug,
        "load" => ParsedLogLevel.Load,
        _ => ParsedLogLevel.Info,
    };
}
