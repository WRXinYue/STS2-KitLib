using KitLog.Cli.Services;
using Spectre.Console;

namespace KitLog.Cli.Rendering;

internal static class LogLineRenderer {
    public static void WriteLine(ParsedLogLine line, bool color) {
        if (!color) {
            Console.WriteLine(line.DisplayText);
            return;
        }

        var markup = line.Level switch {
            ParsedLogLevel.Error => $"[red]{Escape(line.DisplayText)}[/]",
            ParsedLogLevel.Warn => $"[yellow]{Escape(line.DisplayText)}[/]",
            ParsedLogLevel.Debug or ParsedLogLevel.VeryDebug => $"[grey]{Escape(line.DisplayText)}[/]",
            ParsedLogLevel.Load => $"[cyan]{Escape(line.DisplayText)}[/]",
            _ => Escape(line.DisplayText),
        };

        AnsiConsole.MarkupLine(markup);
    }

    static string Escape(string text)
        => text.Replace("[", "[[").Replace("]", "]]");
}
