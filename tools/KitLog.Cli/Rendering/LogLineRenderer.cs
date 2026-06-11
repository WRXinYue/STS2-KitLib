using KitLog.Cli.Services;

namespace KitLog.Cli.Rendering;

internal static class LogLineRenderer {
    public static void WriteLine(ParsedLogLine line, bool color, LogViewerFilterState? viewerState = null) {
        if (!color) {
            Console.WriteLine(line.DisplayText);
            return;
        }

        Console.WriteLine(BuildAnsiLine(line, viewerState));
    }

    static string BuildAnsiLine(ParsedLogLine line, LogViewerFilterState? viewerState) {
        var levelAnsi = AnsiCodes.ForLevel(line.Level);
        var reset = AnsiCodes.Reset;

        if (!TryFindModTag(line.RawLine, viewerState, out int tagStart, out int tagEnd, out var modId))
            return $"{levelAnsi}{line.RawLine}{reset}";

        var modAnsi = AnsiCodes.TrueColorFg(LogModColors.ForId(modId));
        var prefix = line.RawLine[..tagStart];
        var tag = line.RawLine[tagStart..tagEnd];
        var suffix = line.RawLine[tagEnd..];

        if (tagStart == 0)
            return $"{modAnsi}{tag}{reset}{levelAnsi}{suffix}{reset}";

        if (tagEnd >= line.RawLine.Length)
            return $"{levelAnsi}{prefix}{modAnsi}{tag}{reset}";

        return $"{levelAnsi}{prefix}{modAnsi}{tag}{reset}{levelAnsi}{suffix}{reset}";
    }

    static bool TryFindModTag(
        string rawLine,
        LogViewerFilterState? viewerState,
        out int tagStart,
        out int tagEnd,
        out string modId) {
        if (viewerState != null
            && viewerState.LoadedModIds.Count > 0
            && LogViewerFilterMatcher.TryFindModTagSpan(
                rawLine,
                viewerState.LoadedModIds,
                viewerState.ModIdAliases,
                out tagStart,
                out tagEnd,
                out modId))
            return true;

        return LogViewerFilterMatcher.TryFindAnyModTagSpan(rawLine, out tagStart, out tagEnd, out modId);
    }
}
