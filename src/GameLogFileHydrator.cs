using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace KitLib;

/// <summary>
/// Reads and parses this process's mirrored session log, with fallback to shared <c>user://logs/</c>.
/// </summary>
internal static class GameLogFileHydrator {
    private const int MaxReadBytes = 2 * 1024 * 1024;
    private const int MarkerScanTailBytes = 256 * 1024;

    private static readonly Regex TimestampLine = new(
        @"^(?<time>\d{2}:\d{2}:\d{2})\s+(?<level>INFO|WARN|WARNING|ERROR|DEBUG|LOAD|VERYDEBUG|VDB|DBG)\s+(?<text>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BracketLevelLine = new(
        @"^\[(?<level>INFO|WARN|WARNING|ERROR|DEBUG|LOAD|VERYDEBUG|VDB|DBG)\]\s+(?<text>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static string? _cachedSessionLogPath;

    internal static string LogsDirectory => Path.Combine(OS.GetUserDataDir(), "logs");

    internal static string? CurrentSessionLogPath => FindSessionLogPath();

    internal static string? CurrentSessionLogDisplayName {
        get {
            var path = FindSessionLogPath();
            if (path == null)
                return null;
            if (InstanceLogWriter.IsActive
                && string.Equals(path, InstanceLogWriter.SessionLogPath, StringComparison.OrdinalIgnoreCase))
                return InstanceLogWriter.DisplayName;
            return Path.GetFileName(path);
        }
    }

    internal static string? CurrentSessionLogFileName => CurrentSessionLogDisplayName;

    internal static List<LogCollector.Entry> ReadSessionLogEntries() {
        var path = FindSessionLogPath();
        if (path == null)
            return [];

        try {
            var referenceDate = File.GetLastWriteTime(path).Date;
            return ParseFile(path, referenceDate);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[LogViewer] Failed to read session log file: {ex.Message}");
            return [];
        }
    }

    internal static string? FindSessionLogPath() {
        if (_cachedSessionLogPath != null && File.Exists(_cachedSessionLogPath))
            return _cachedSessionLogPath;

        if (InstanceLogWriter.IsActive && File.Exists(InstanceLogWriter.SessionLogPath)) {
            _cachedSessionLogPath = InstanceLogWriter.SessionLogPath;
            return _cachedSessionLogPath;
        }

        var resolved = ScanForSessionLogPath();
        if (resolved != null)
            _cachedSessionLogPath = resolved;
        return resolved;
    }

    internal static void InvalidateSessionLogPathCache() => _cachedSessionLogPath = null;

    /// <summary>All log files newest-first; marks the file bound to this process.</summary>
    internal static IReadOnlyList<(string DisplayName, string AbsPath, bool IsCurrentSession)> ScanLogFiles() {
        try {
            var current = FindSessionLogPath();
            var currentTag = I18N.T("log.instance.currentFileTag", "(this window)");
            var rows = new List<(string Name, string Path, bool IsCurrent, DateTime WriteTime)>();

            if (InstanceLogWriter.IsActive && File.Exists(InstanceLogWriter.SessionLogPath)) {
                var path = InstanceLogWriter.SessionLogPath;
                bool isCurrent = current != null
                    && string.Equals(path, current, StringComparison.OrdinalIgnoreCase);
                var name = InstanceLogWriter.DisplayName + (isCurrent ? " " + currentTag : "");
                rows.Add((name, path, isCurrent, File.GetLastWriteTime(path)));
            }

            var logsDir = LogsDirectory;
            if (Directory.Exists(logsDir)) {
                foreach (var path in Directory.GetFiles(logsDir)) {
                    var name = Path.GetFileName(path);
                    bool isCurrent = current != null
                        && string.Equals(path, current, StringComparison.OrdinalIgnoreCase);
                    if (isCurrent)
                        name += " " + currentTag;
                    rows.Add((name, path, isCurrent, File.GetLastWriteTime(path)));
                }
            }

            return rows
                .OrderByDescending(r => r.IsCurrent)
                .ThenByDescending(r => r.WriteTime)
                .Select(r => (r.Name, r.Path, r.IsCurrent))
                .ToList();
        }
        catch {
            return Array.Empty<(string, string, bool)>();
        }
    }

    private static string? ScanForSessionLogPath() {
        try {
            var logsDir = LogsDirectory;
            if (!Directory.Exists(logsDir))
                return null;

            string? bestPath = null;
            DateTime bestTime = DateTime.MinValue;

            foreach (var path in Directory.EnumerateFiles(logsDir)) {
                if (!TailContainsSessionMarker(path))
                    continue;

                var writeTime = File.GetLastWriteTime(path);
                if (writeTime < bestTime)
                    continue;

                bestTime = writeTime;
                bestPath = path;
            }

            return bestPath;
        }
        catch {
            return null;
        }
    }

    private static bool TailContainsSessionMarker(string path) {
        try {
            using var fs = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0)
                return false;

            var readLen = (int)Math.Min(MarkerScanTailBytes, fs.Length);
            fs.Seek(-readLen, SeekOrigin.End);
            var buffer = new byte[readLen];
            var offset = 0;
            while (offset < readLen) {
                int n = fs.Read(buffer, offset, readLen - offset);
                if (n <= 0)
                    break;
                offset += n;
            }

            var text = Encoding.UTF8.GetString(buffer);
            return KitLibInstance.ContainsSessionBoundary(text);
        }
        catch {
            return false;
        }
    }

    private static List<LogCollector.Entry> ParseFile(string path, DateTime referenceDate) {
        var lines = ReadTailLines(path);
        var entries = new List<LogCollector.Entry>(lines.Count);

        foreach (var line in lines) {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!TryParseLine(line, referenceDate, out var entry))
                continue;

            entries.Add(entry);
        }

        return entries;
    }

    private static List<string> ReadTailLines(string path) {
        using var fs = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
        var truncated = fs.Length > MaxReadBytes;
        if (truncated)
            fs.Seek(-MaxReadBytes, SeekOrigin.End);

        using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        if (truncated)
            reader.ReadLine();

        var content = reader.ReadToEnd();
        if (string.IsNullOrEmpty(content))
            return [];

        var lines = new List<string>();
        foreach (var line in content.Split('\n')) {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length > 0)
                lines.Add(trimmed);
        }

        return lines;
    }

    private static bool TryParseLine(string line, DateTime referenceDate, out LogCollector.Entry entry) {
        entry = default;

        Match match = TimestampLine.Match(line);
        DateTime time;
        string levelToken;
        bool hasWallClockTime;

        if (match.Success) {
            if (!TimeSpan.TryParse(match.Groups["time"].Value, out var tod))
                return false;

            time = referenceDate + tod;
            if (time > DateTime.Now.AddMinutes(1))
                time = time.AddDays(-1);

            levelToken = match.Groups["level"].Value;
            hasWallClockTime = true;
        }
        else {
            match = BracketLevelLine.Match(line);
            if (match.Success) {
                time = default;
                levelToken = match.Groups["level"].Value;
                hasWallClockTime = false;
            }
            else {
                entry = new LogCollector.Entry(
                    LogLevel.Info,
                    line,
                    default,
                    IsFromFile: true,
                    HasWallClockTime: false);
                return true;
            }
        }

        if (!TryParseLevel(levelToken, out var level))
            return false;

        entry = new LogCollector.Entry(level, line, time, IsFromFile: true, HasWallClockTime: hasWallClockTime);
        return true;
    }

    private static bool TryParseLevel(string token, out LogLevel level) {
        level = LogLevel.Info;
        switch (token.ToUpperInvariant()) {
            case "ERROR":
                level = LogLevel.Error;
                return true;
            case "WARN":
            case "WARNING":
                level = LogLevel.Warn;
                return true;
            case "INFO":
                level = LogLevel.Info;
                return true;
            case "LOAD":
                level = LogLevel.Load;
                return true;
            case "DEBUG":
            case "DBG":
                level = LogLevel.Debug;
                return true;
            case "VERYDEBUG":
            case "VDB":
                level = LogLevel.VeryDebug;
                return true;
            default:
                return false;
        }
    }
}
