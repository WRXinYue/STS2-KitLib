using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.CombatStats;

/// <summary>Maps stored model id keys (e.g. VULNERABLE_POWER) to localized display names.</summary>
internal static class CombatStatsDisplayNames {
    private static readonly Dictionary<string, string> Cache = new();

    public static string LocalizeKey(string key) {
        if (string.IsNullOrWhiteSpace(key))
            return key;
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        string resolved = ResolveModelName(key) ?? key;
        Cache[key] = resolved;
        return resolved;
    }

    public static string LocalizeEventText(string text) {
        if (string.IsNullOrEmpty(text))
            return text;

        const string arrow = " → ";
        int idx = text.IndexOf(arrow, StringComparison.Ordinal);
        if (idx > 0)
            return $"{LocalizeKey(text[..idx])}{arrow}{text[(idx + arrow.Length)..]}";

        return LocalizeKey(text);
    }

    private static string? ResolveModelName(string entry) {
        var card = ModelDb.AllCards.FirstOrDefault(c => ((AbstractModel)c).Id.Entry == entry);
        if (card != null)
            return CardEditActions.GetCardDisplayName(card);

        var power = ModelDb.AllPowers.FirstOrDefault(p => ((AbstractModel)p).Id.Entry == entry);
        if (power != null)
            return PowerActions.GetPowerDisplayName(power);

        var potion = ModelDb.AllPotions.FirstOrDefault(p => ((AbstractModel)p).Id.Entry == entry);
        if (potion != null)
            return PotionActions.GetPotionDisplayName(potion);

        var monster = ModelDb.Monsters.FirstOrDefault(m => ((AbstractModel)m).Id.Entry == entry);
        if (monster != null) {
            try {
                string? title = monster.Title?.GetFormattedText();
                if (!string.IsNullOrWhiteSpace(title))
                    return title;
            }
            catch {
                // fall through
            }
        }

        return null;
    }
}
