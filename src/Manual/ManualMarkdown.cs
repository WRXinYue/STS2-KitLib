using System;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

namespace KitLib.UI;

/// <summary>Minimal Markdown → Godot BBCode for in-game manual pages.</summary>
internal static partial class ManualMarkdown {
    public static string ToBbcode(string markdown) {
        if (string.IsNullOrWhiteSpace(markdown))
            return "";

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        var inCode = false;
        var codeLang = "";

        foreach (var raw in lines) {
            var line = raw;
            if (line.StartsWith("```", StringComparison.Ordinal)) {
                if (!inCode) {
                    inCode = true;
                    codeLang = line.Length > 3 ? line[3..].Trim() : "";
                    sb.Append("[code]");
                    if (!string.IsNullOrEmpty(codeLang))
                        sb.Append('\n');
                }
                else {
                    inCode = false;
                    sb.Append("[/code]\n");
                }
                continue;
            }

            if (inCode) {
                sb.Append(line).Append('\n');
                continue;
            }

            if (string.IsNullOrWhiteSpace(line)) {
                sb.Append('\n');
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal)) {
                AppendHeading(sb, line[4..], 12);
                continue;
            }
            if (line.StartsWith("## ", StringComparison.Ordinal)) {
                AppendHeading(sb, line[3..], 13);
                continue;
            }
            if (line.StartsWith("# ", StringComparison.Ordinal)) {
                AppendHeading(sb, line[2..], 15);
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal)) {
                sb.Append("  • ").Append(InlineFormat(line[2..])).Append('\n');
                continue;
            }

            if (Regex.IsMatch(line, @"^\d+\.\s")) {
                sb.Append(' ').Append(InlineFormat(line)).Append('\n');
                continue;
            }

            if (line.StartsWith("---", StringComparison.Ordinal)) {
                sb.Append("[color=#444444]────────────────[/color]\n");
                continue;
            }

            sb.Append(InlineFormat(line)).Append('\n');
        }

        if (inCode)
            sb.Append("[/code]\n");

        return sb.ToString().TrimEnd();
    }

    private static void AppendHeading(StringBuilder sb, string text, int size) {
        const string hex = "5b9eff";
        sb.Append("[font_size=").Append(size).Append("][color=").Append(hex).Append(']')
            .Append(InlineFormat(text))
            .Append("[/color][/font_size]\n");
    }

    private static string InlineFormat(string text) {
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1");
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "[b]$1[/b]");
        text = Regex.Replace(text, @"`([^`]+)`", "[code]$1[/code]");
        return text;
    }
}
