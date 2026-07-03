namespace KitLib.Abstractions.Compat;

public static class Sts2GameVersion {
    public static bool TryParseCore(string text, out Version version) {
        var s = text.Trim();
        var dash = s.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
            s = s[..dash].Trim();
        var plus = s.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
            s = s[..plus].Trim();
        if (s.Length >= 2 && (s[0] == 'v' || s[0] == 'V') && char.IsDigit(s[1]))
            s = s[1..];
        if (Version.TryParse(s, out var parsed)) {
            version = parsed;
            return true;
        }

        version = new(0, 0);
        return false;
    }
}
