using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace KitLib.UI;

internal static class ManualContent {
    public sealed record Topic(string Id, string TitleKey);

    /// <summary>One manual page per rail panel (excluding dev tools and the manual tab itself).</summary>
    public static readonly IReadOnlyList<Topic> Topics = new Topic[]
    {
        new("cards", "panel.cards"),
        new("relics", "panel.relics"),
        new("enemies", "panel.enemies"),
        new("powers", "panel.powers"),
        new("potions", "panel.potions"),
        new("events", "panel.events"),
        new("rooms", "panel.rooms"),
        new("cheats", "panel.cheats"),
        new("enemyIntent", "panel.enemyIntent"),
        new("combatStats", "panel.combatStats"),
        new("presets", "panel.presets"),
        new("save", "panel.save"),
        new("settings", "panel.settings"),
        new("feedback", "panel.feedback"),
    };

    public static string LoadMarkdown(string topicId) {
        var lang = I18N.LangCode;
        var resourceName = $"KitLib.Manual.{lang}.{topicId}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null) {
            MainFile.Logger.Warn($"[ManualContent] Missing manual topic '{topicId}' for '{lang}'.");
            return "";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
