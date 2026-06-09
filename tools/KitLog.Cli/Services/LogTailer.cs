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

        using var fs = new FileStream(
            options.FilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        var startPosition = SeekTailStart(fs, options.TailLines);
        fs.Seek(startPosition, SeekOrigin.Begin);

        using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        if (startPosition > 0)
            reader.ReadLine();

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null) {
            if (!PassesFilter(line, filter))
                continue;

            var parsed = LogLineParser.Parse(line);
            if (!LogLineParser.MeetsMinimumLevel(parsed.Level, options.MinimumLevel))
                continue;

            LogLineRenderer.WriteLine(parsed, options.Color);
        }

        if (!options.Follow)
            return 0;

        while (!ct.IsCancellationRequested) {
            line = await reader.ReadLineAsync(ct);
            if (line == null) {
                await Task.Delay(200, ct);
                continue;
            }

            if (!PassesFilter(line, filter))
                continue;

            var parsed = LogLineParser.Parse(line);
            if (!LogLineParser.MeetsMinimumLevel(parsed.Level, options.MinimumLevel))
                continue;

            LogLineRenderer.WriteLine(parsed, options.Color);
        }

        return 0;
    }

    static bool PassesFilter(string line, Regex? filter)
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
