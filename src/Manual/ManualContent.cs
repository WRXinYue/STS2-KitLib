using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DevMode.UI;

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

    private static readonly string ModDir =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

    public static string LoadMarkdown(string topicId) {
        var lang = I18N.LangCode;
        var external = Path.Combine(ModDir, "manual", lang, $"{topicId}.md");
        if (File.Exists(external)) {
            try {
                return File.ReadAllText(external);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[ManualContent] Failed to read '{external}': {ex.Message}");
            }
        }

        var resourceName = $"DevMode.Manual.{lang}.{topicId}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null) {
            MainFile.Logger.Warn($"[ManualContent] Missing manual topic '{topicId}' for '{lang}'.");
            return "";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
