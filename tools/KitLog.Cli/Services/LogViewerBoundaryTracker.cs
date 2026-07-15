using System.Text;

namespace KitLog.Cli.Services;

/// <summary>
/// Mirrors the in-game viewer: lines before the last session boundary are shown without viewer filters.
/// </summary>
internal sealed class LogViewerBoundaryTracker {
    bool _applyViewerFilters;

    public LogViewerBoundaryTracker(string logPath, long tailStartOffset) {
        var lastBoundaryEnd = FindLastBoundaryEndOffset(logPath);
        if (lastBoundaryEnd < 0) {
            _applyViewerFilters = false;
            return;
        }

        _applyViewerFilters = tailStartOffset >= lastBoundaryEnd;
    }

    public bool ApplyViewerFilters => _applyViewerFilters;

    public void OnLine(string line) {
        if (LogViewerFilterMatcher.IsSessionBoundary(line))
            _applyViewerFilters = true;
    }

    internal static long FindLastBoundaryEndOffset(string logPath) {
        try {
            using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            long lastEnd = -1;
            string? line;
            while ((line = reader.ReadLine()) != null) {
                if (!LogViewerFilterMatcher.IsSessionBoundary(line))
                    continue;

                lastEnd = fs.Position;
            }

            return lastEnd;
        }
        catch {
            return -1;
        }
    }

}
