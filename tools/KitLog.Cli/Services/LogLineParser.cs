using System.Text.RegularExpressions;

namespace KitLog.Cli.Services;

internal enum ParsedLogLevel {
    Debug,
    Info,
    Warn,
    Error,
    Load,
    VeryDebug,
    Unknown,
}

internal readonly record struct ParsedLogLine(
    ParsedLogLevel Level,
    string RawLine,
    string DisplayText,
    bool HasWallClockTime);

internal static class LogLineParser {
    static readonly Regex TimestampLine = new(
        @"^(?<time>\d{2}:\d{2}:\d{2})\s+(?<level>INFO|WARN|WARNING|ERROR|DEBUG|LOAD|VERYDEBUG|VDB|DBG)\s+(?<text>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    static readonly Regex BracketLevelLine = new(
        @"^\[(?<level>INFO|WARN|WARNING|ERROR|DEBUG|LOAD|VERYDEBUG|VDB|DBG)\]\s+(?<text>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static ParsedLogLine Parse(string line) {
        var match = TimestampLine.Match(line);
        if (match.Success) {
            var level = ParseLevelToken(match.Groups["level"].Value);
            return new ParsedLogLine(level, line, line, HasWallClockTime: true);
        }

        match = BracketLevelLine.Match(line);
        if (match.Success) {
            var level = ParseLevelToken(match.Groups["level"].Value);
            return new ParsedLogLine(level, line, line, HasWallClockTime: false);
        }

        return new ParsedLogLine(ParsedLogLevel.Info, line, line, HasWallClockTime: false);
    }

    public static bool MeetsMinimumLevel(ParsedLogLevel level, ParsedLogLevel? minimum) {
        if (minimum is not ParsedLogLevel min)
            return true;

        return Severity(level) >= Severity(min);
    }

    static int Severity(ParsedLogLevel level) => level switch {
        ParsedLogLevel.Error => 4,
        ParsedLogLevel.Warn => 3,
        ParsedLogLevel.Info => 2,
        ParsedLogLevel.Load => 2,
        ParsedLogLevel.Debug => 1,
        ParsedLogLevel.VeryDebug => 0,
        _ => 1,
    };

    static ParsedLogLevel ParseLevelToken(string token) => token.ToUpperInvariant() switch {
        "ERROR" => ParsedLogLevel.Error,
        "WARN" or "WARNING" => ParsedLogLevel.Warn,
        "INFO" => ParsedLogLevel.Info,
        "LOAD" => ParsedLogLevel.Load,
        "DEBUG" or "DBG" => ParsedLogLevel.Debug,
        "VERYDEBUG" or "VDB" => ParsedLogLevel.VeryDebug,
        _ => ParsedLogLevel.Unknown,
    };
}
