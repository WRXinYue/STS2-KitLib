namespace KitLib.Abstractions.Modding;

/// <summary>Reads <see cref="KitLibCompatDocument.FileName"/> from mod metadata sidecars.</summary>
public static class KitLibCompatTomlReader {
    public static bool TryReadFile(string modDirectory, out KitLibCompatDocument? document) {
        document = null;
        if (string.IsNullOrWhiteSpace(modDirectory))
            return false;
        var path = Path.Combine(modDirectory, KitLibCompatDocument.FileName);
        if (!File.Exists(path))
            return false;
        try {
            return TryParse(File.ReadAllText(path), out document);
        }
        catch {
            return false;
        }
    }

    public static bool TryParse(string text, out KitLibCompatDocument? document) {
        document = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string? section = null;
        var gameRanges = new List<string>();
        var kitlibRanges = new List<string>();
        var modules = new List<string>();

        foreach (var rawLine in text.Split('\n')) {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith('[') && line.EndsWith(']')) {
                section = line[1..^1].Trim();
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq < 0)
                continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            var parsed = ParseTomlValue(value);
            if (parsed.Count == 0)
                continue;

            if (string.Equals(section, "game", StringComparison.OrdinalIgnoreCase)
                && string.Equals(key, "version", StringComparison.OrdinalIgnoreCase)) {
                gameRanges.AddRange(parsed);
            }
            else if (string.Equals(section, "kitlib", StringComparison.OrdinalIgnoreCase)
                     && string.Equals(key, "version", StringComparison.OrdinalIgnoreCase)) {
                kitlibRanges.AddRange(parsed);
            }
            else if (string.Equals(section, "kitlib", StringComparison.OrdinalIgnoreCase)
                     && string.Equals(key, "modules", StringComparison.OrdinalIgnoreCase)) {
                modules.AddRange(parsed);
            }
        }

        if (gameRanges.Count == 0 && kitlibRanges.Count == 0 && modules.Count == 0)
            return false;

        document = new KitLibCompatDocument {
            GameVersionRanges = gameRanges,
            KitLibVersionRanges = kitlibRanges,
            KitLibModules = modules,
        };
        return true;
    }

    static string StripComment(string line) {
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++) {
            if (line[i] == '"')
                inQuotes = !inQuotes;
            if (!inQuotes && line[i] == '#')
                return line[..i];
        }
        return line;
    }

    static IReadOnlyList<string> ParseTomlValue(string value) {
        if (value.Length == 0)
            return [];
        if (value.StartsWith('['))
            return ParseQuotedStrings(value);
        if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
            return [Unquote(value)];
        return [];
    }

    static IReadOnlyList<string> ParseQuotedStrings(string arrayText) {
        var items = new List<string>();
        var i = 0;
        while (i < arrayText.Length) {
            if (arrayText[i] != '"') {
                i++;
                continue;
            }
            var end = i + 1;
            while (end < arrayText.Length) {
                if (arrayText[end] == '"' && arrayText[end - 1] != '\\')
                    break;
                end++;
            }
            if (end >= arrayText.Length)
                break;
            items.Add(Unquote(arrayText[i..(end + 1)]));
            i = end + 1;
        }
        return items;
    }

    static string Unquote(string quoted) {
        var text = quoted.Trim();
        if (text.Length >= 2 && text.StartsWith('"') && text.EndsWith('"'))
            return text[1..^1].Trim();
        return text;
    }
}
