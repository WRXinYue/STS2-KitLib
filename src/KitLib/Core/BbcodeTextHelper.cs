using System.Text.RegularExpressions;

namespace KitLib;

/// <summary>BBCode stripping helpers shared across Dev and Panel modules.</summary>
public static class BbcodeTextHelper {
    private static readonly (string tag, string hex)[] ColorTags =
    {
        ("gold",   "#EFC851"),
        ("blue",   "#87CEEB"),
        ("red",    "#FF5555"),
        ("green",  "#7FFF00"),
        ("purple", "#EE82EE"),
        ("orange", "#FFA518"),
        ("aqua",   "#2AEBBE"),
        ("pink",   "#FF78A0"),
    };

    private static readonly string[] EffectOnlyTags =
        { "sine", "jitter", "fade_in", "fly_in", "thinky_dots", "ancient_banner" };

    public static string StripFontSizeBbcode(string text) {
        if (string.IsNullOrEmpty(text))
            return text;
        return Regex.Replace(text, @"\[/?font_size(?:=\d+)?\]", string.Empty);
    }

    public static string ToPlainTooltipText(string text) {
        if (string.IsNullOrEmpty(text))
            return text;

        text = StripFontSizeBbcode(text);

        foreach (var (tag, _) in ColorTags) {
            text = text.Replace($"[{tag}]", string.Empty);
            text = text.Replace($"[/{tag}]", string.Empty);
        }

        foreach (var tag in EffectOnlyTags) {
            text = text.Replace($"[{tag}]", string.Empty);
            text = text.Replace($"[/{tag}]", string.Empty);
        }

        text = Regex.Replace(text, @"\[color=[^\]]+\]", string.Empty);
        text = text.Replace("[/color]", string.Empty);
        text = Regex.Replace(text, @"\[/?(?:center|left|right|fill|b|i|u|s|code|font|url=[^\]]+)\]", string.Empty);
        return text.Trim();
    }
}
