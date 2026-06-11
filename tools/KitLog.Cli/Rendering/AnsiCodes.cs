using KitLog.Cli.Services;

namespace KitLog.Cli.Rendering;

/// <summary>ANSI colors aligned with in-game LogViewerUI BBCode palette.</summary>
internal static class AnsiCodes {
    public const string Reset = "\x1b[0m";

    public static string ForLevel(ParsedLogLevel level) => level switch {
        ParsedLogLevel.Error => TrueColorFg("FF5F5F"),
        ParsedLogLevel.Warn => TrueColorFg("FFC840"),
        ParsedLogLevel.Debug or ParsedLogLevel.VeryDebug => TrueColorFg("6A6A8A"),
        ParsedLogLevel.Load => TrueColorFg("7ADCDC"),
        _ => TrueColorFg("C8C8DC"),
    };

    public static string TrueColorFg(string hexRgb) {
        if (hexRgb.Length != 6)
            return "\x1b[37m";

        var r = Convert.ToInt32(hexRgb[..2], 16);
        var g = Convert.ToInt32(hexRgb[2..4], 16);
        var b = Convert.ToInt32(hexRgb[4..6], 16);
        return $"\x1b[38;2;{r};{g};{b}m";
    }

    public static void WriteStatusLine(string message) =>
        Console.WriteLine($"{TrueColorFg("6A6A8A")}{message}{Reset}");
}
