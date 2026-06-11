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
        var sb = new System.Text.StringBuilder(line.RawLine.Length + 32);

        AppendSegment(sb, levelAnsi, line.RawLine[..tagStart], reset);
        AppendSegment(sb, modAnsi, line.RawLine[tagStart..tagEnd], reset);

        int pos = tagEnd;
        var loadedModIds = viewerState?.LoadedModIds;
        var modIdAliases = viewerState?.ModIdAliases;
        while (LogViewerFilterMatcher.TryFindSecondaryTagSpan(
                   line.RawLine,
                   pos,
                   modId,
                   loadedModIds,
                   modIdAliases,
                   out int secondaryStart,
                   out int secondaryEnd,
                   out bool isContentModTag,
                   out var secondaryInner)) {
            AppendSegment(sb, levelAnsi, line.RawLine[pos..secondaryStart], reset);

            var secondaryAnsi = isContentModTag
                ? AnsiCodes.TrueColorFg(LogModColors.ForId(secondaryInner))
                : AnsiCodes.ScopeDim;
            AppendSegment(sb, secondaryAnsi, line.RawLine[secondaryStart..secondaryEnd], reset);
            pos = secondaryEnd;
        }

        AppendSegment(sb, levelAnsi, line.RawLine[pos..], reset);
        return sb.ToString();
    }

    static void AppendSegment(System.Text.StringBuilder sb, string ansi, string text, string reset) {
        if (text.Length == 0)
            return;
        sb.Append(ansi).Append(text).Append(reset);
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
