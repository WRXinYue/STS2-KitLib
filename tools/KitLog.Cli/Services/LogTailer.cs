using System.Text;
using System.Text.RegularExpressions;
using KitLog.Cli.Rendering;
using Spectre.Console;

namespace KitLog.Cli.Services;

internal sealed class LogTailOptions {
    public required string FilePath { get; init; }
    public bool Follow { get; init; }
    public int TailLines { get; init; } = 40;
    public string? FilterPattern { get; init; }
    public ParsedLogLevel? MinimumLevel { get; init; }
    public bool Color { get; init; } = true;
    public bool SyncViewer { get; init; }
    public int? Pid { get; init; }
}

internal static class LogTailer {
    public static async Task<int> RunAsync(LogTailOptions options, CancellationToken ct) {
        if (!File.Exists(options.FilePath)) {
            AnsiConsole.MarkupLine($"[red]Log file not found:[/] {Markup.Escape(options.FilePath)}");
            return 1;
        }

        Regex? filter = null;
        if (!string.IsNullOrWhiteSpace(options.FilterPattern)) {
            try {
                filter = new Regex(options.FilterPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            }
            catch (Exception ex) {
                AnsiConsole.MarkupLine($"[red]Invalid filter regex:[/] {Markup.Escape(ex.Message)}");
                return 1;
            }
        }

        LogViewerFilterWatcher? viewerWatcher = null;
        if (options.SyncViewer)
            viewerWatcher = new LogViewerFilterWatcher();

        using var fs = new FileStream(
            options.FilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        var startPosition = ResolveStartPosition(fs, options);
        var boundaryTracker = new LogViewerBoundaryTracker(options.FilePath, startPosition);
        fs.Seek(startPosition, SeekOrigin.Begin);

        using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        if (startPosition > 0)
            reader.ReadLine();

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null) {
            if (!ShouldEmitLine(line, filter, options, viewerWatcher, boundaryTracker))
                continue;

            var parsed = LogLineParser.Parse(line);
            LogLineRenderer.WriteLine(parsed, options.Color, viewerWatcher?.Current);
        }

        if (!options.Follow)
            return 0;

        while (!ct.IsCancellationRequested) {
            line = await reader.ReadLineAsync(ct);
            if (line == null) {
                await Task.Delay(200, ct);
                continue;
            }

            if (!ShouldEmitLine(line, filter, options, viewerWatcher, boundaryTracker))
                continue;

            var parsed = LogLineParser.Parse(line);
            LogLineRenderer.WriteLine(parsed, options.Color, viewerWatcher?.Current);
        }

        return 0;
    }

    static long ResolveStartPosition(FileStream fs, LogTailOptions options) {
        if (options.SyncViewer) {
            var boundary = LogViewerBoundaryTracker.FindLastBoundaryEndOffset(options.FilePath);
            if (boundary >= 0)
                return boundary;
        }

        return SeekTailStart(fs, options.TailLines);
    }

    static bool ShouldEmitLine(
        string line,
        Regex? regexFilter,
        LogTailOptions options,
        LogViewerFilterWatcher? viewerWatcher,
        LogViewerBoundaryTracker boundaryTracker) {
        boundaryTracker.OnLine(line);

        if (!PassesRegexFilter(line, regexFilter))
            return false;

        if (options.SyncViewer && viewerWatcher != null) {
            var viewerState = viewerWatcher.Current;
            var parsed = LogLineParser.Parse(line);
            if (!LogViewerFilterMatcher.ShouldShow(
                    line,
                    parsed,
                    viewerState,
                    boundaryTracker.ApplyViewerFilters))
                return false;

            return true;
        }

        var fallbackParsed = LogLineParser.Parse(line);
        return LogLineParser.MeetsMinimumLevel(fallbackParsed.Level, options.MinimumLevel);
    }

    static bool PassesRegexFilter(string line, Regex? filter)
        => filter == null || filter.IsMatch(line);

    static long SeekTailStart(FileStream fs, int tailLines) {
        if (tailLines <= 0 || fs.Length == 0)
            return 0;

        const int chunkSize = 4096;
        var remaining = tailLines;
        long position = fs.Length;

        while (position > 0 && remaining > 0) {
            var readSize = (int)Math.Min(chunkSize, position);
            position -= readSize;
            fs.Seek(position, SeekOrigin.Begin);

            var buffer = new byte[readSize];
            var read = fs.Read(buffer, 0, readSize);
            for (var i = read - 1; i >= 0; i--) {
                if (buffer[i] != (byte)'\n')
                    continue;

                remaining--;
                if (remaining == 0)
                    return position + i + 1;
            }
        }

        return 0;
    }
}
