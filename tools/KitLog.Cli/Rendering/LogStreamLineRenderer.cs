using KitLib.Logging;
using KitLog.Cli.Services;

namespace KitLog.Cli.Rendering;

internal static class LogStreamLineRenderer {
    const string DefaultHostModId = "KitLib";

    public static void WriteLine(LogStreamEntry entry, bool color, LogViewerFilterState? viewerState = null) {
        if (!color) {
            Console.WriteLine(FormatPlain(entry));
            return;
        }

        if (entry.Boundary) {
            Console.WriteLine(BuildBoundaryAnsiLine(entry));
            return;
        }

        // Game LogCallback frames omit mod/scope; parse tags from text like session.log tail.
        if (string.IsNullOrEmpty(entry.Mod)) {
            LogLineRenderer.WriteLine(LogStreamLineAdapter.ToParsedLine(entry), color: true, viewerState);
            return;
        }

        Console.WriteLine(BuildStructuredAnsiLine(entry));
    }

    public static string FormatPlain(LogStreamEntry entry) {
        var time = FormatLocalTime(entry.Ts);
        return $"{time} {LevelBadge(entry.Lvl)} {entry.Text}";
    }

    static string BuildBoundaryAnsiLine(LogStreamEntry entry) {
        var header = $"{FormatLocalTime(entry.Ts)} {LevelBadge(entry.Lvl)} ";
        var reset = AnsiCodes.Reset;
        return $"{AnsiCodes.TrueColorFg("7ADCDC")}{header}{entry.Text}{reset}";
    }

    static string BuildStructuredAnsiLine(LogStreamEntry entry) {
        var levelAnsi = AnsiCodes.ForStreamLevel(entry.Lvl);
        var reset = AnsiCodes.Reset;
        var header = $"{FormatLocalTime(entry.Ts)} {LevelBadge(entry.Lvl)} ";

        var modAnsi = AnsiCodes.TrueColorFg(LogModColors.ForId(entry.Mod));
        var sb = new System.Text.StringBuilder(entry.Text.Length + header.Length + 32);
        AppendSegment(sb, levelAnsi, header, reset);

        int pos = 0;
        var hostTag = $"[{DefaultHostModId}]";
        if (entry.Text.StartsWith(hostTag, StringComparison.OrdinalIgnoreCase)) {
            AppendSegment(sb, modAnsi, hostTag, reset);
            pos = hostTag.Length;
            pos = SkipWhitespace(entry.Text, pos);
        }

        var modTag = $"[{entry.Mod}]";
        if (!entry.Mod.Equals(DefaultHostModId, StringComparison.OrdinalIgnoreCase)
            && entry.Text.AsSpan(pos).StartsWith(modTag, StringComparison.Ordinal)) {
            AppendSegment(sb, modAnsi, modTag, reset);
            pos += modTag.Length;
        }

        if (!string.IsNullOrEmpty(entry.Scope)) {
            int gapStart = pos;
            pos = SkipWhitespace(entry.Text, pos);
            AppendSegment(sb, levelAnsi, entry.Text[gapStart..pos], reset);

            var scopeTag = $"[{entry.Scope}]";
            if (entry.Text.AsSpan(pos).StartsWith(scopeTag, StringComparison.Ordinal)) {
                AppendSegment(sb, AnsiCodes.ScopeDim, scopeTag, reset);
                pos += scopeTag.Length;
            }
        }

        AppendSegment(sb, levelAnsi, entry.Text[pos..], reset);
        return sb.ToString();
    }

    static int SkipWhitespace(string text, int pos) {
        while (pos < text.Length && char.IsWhiteSpace(text[pos]))
            pos++;
        return pos;
    }

    static void AppendSegment(System.Text.StringBuilder sb, string ansi, string text, string reset) {
        if (text.Length == 0)
            return;
        sb.Append(ansi).Append(text).Append(reset);
    }

    static string FormatLocalTime(long unixMs)
        => DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime().ToString("HH:mm:ss");

    static string LevelBadge(string lvl) => lvl.ToLowerInvariant() switch {
        "error" => "ERROR",
        "warn" or "warning" => "WARN",
        "debug" or "dbg" => "DEBUG",
        "vdb" or "verydebug" => "VDB",
        "load" => "LOAD",
        _ => "INFO",
    };
}
