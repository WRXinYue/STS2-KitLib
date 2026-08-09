using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.CombatStats;

/// <summary>Maps model instances and id keys to localized display names for combat stats.</summary>
internal static class CombatStatsDisplayNames {
    private static readonly Dictionary<string, string> Cache = new();

    public static string ResolveCardName(CardModel? card) {
        if (card == null)
            return "";
        try {
            return CardEditActions.GetCardDisplayName(card);
        }
        catch {
            return ((AbstractModel)card).Id.Entry ?? "";
        }
    }

    public static string ResolvePowerName(PowerModel power) {
        try {
            return PowerActions.GetPowerDisplayName(power);
        }
        catch {
            return ((AbstractModel)power).Id.Entry ?? "?";
        }
    }

    public static string ResolvePotionName(PotionModel potion) {
        try {
            return PotionActions.GetPotionDisplayName(potion);
        }
        catch {
            return ((AbstractModel)potion).Id.Entry ?? "?";
        }
    }

    public static string ResolveRelicName(RelicModel relic) {
        try {
            string? title = relic.Title?.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }
        catch {
            // fall through
        }

        try {
            return ((AbstractModel)relic).Id.Entry ?? "?";
        }
        catch {
            return "?";
        }
    }

    public static string LocalizeSourceName(CombatStatSourceKind kind, string name) {
        if (string.IsNullOrWhiteSpace(name))
            return name;
        if (kind is CombatStatSourceKind.Card or CombatStatSourceKind.Power
            or CombatStatSourceKind.Relic or CombatStatSourceKind.Potion
            or CombatStatSourceKind.Enemy or CombatStatSourceKind.Synergy)
            return LocalizeKey(name);
        return name;
    }

    public static PowerState CapturePowerState(PowerModel power) {
        string entry = "";
        try {
            entry = power.Id.Entry ?? "";
        }
        catch {
            // keep empty entry
        }

        return new PowerState {
            Id = entry,
            DisplayName = ResolvePowerName(power),
            Amount = power.Amount,
        };
    }

    public static string LocalizeKey(string key) {
        if (string.IsNullOrWhiteSpace(key))
            return key;
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        string resolved = ResolveModelName(key) ?? key;
        Cache[key] = resolved;
        return resolved;
    }

    public static string FormatTimelineLine(CombatStatEvent ev) {
        string line = $"T{ev.Turn} · {LocalizeEventKind(ev.Kind)} · {LocalizeEventText(ev.Text)}";
        return ev.Amount > 0 ? $"{line} ({ev.Amount})" : line;
    }

    public static string LocalizeEventKind(CombatStatEventKind kind) => kind switch {
        CombatStatEventKind.DamageDealt => I18N.T("combatStats.event.damageDealt", "Damage"),
        CombatStatEventKind.DamageTaken => I18N.T("combatStats.event.damageTaken", "Damage taken"),
        CombatStatEventKind.BlockGained => I18N.T("combatStats.event.block", "Block"),
        CombatStatEventKind.CardPlayed => I18N.T("combatStats.event.card", "Card"),
        CombatStatEventKind.EnergySpent => I18N.T("combatStats.event.energy", "Energy"),
        CombatStatEventKind.PotionUsed => I18N.T("combatStats.event.potion", "Potion"),
        CombatStatEventKind.DebuffApplied => I18N.T("combatStats.event.debuff", "Debuff"),
        CombatStatEventKind.BuffApplied => I18N.T("combatStats.event.buff", "Buff"),
        CombatStatEventKind.PowerSynergy => I18N.T("combatStats.event.synergy", "Synergy"),
        CombatStatEventKind.EnemyMove => I18N.T("combatStats.event.enemy", "Enemy"),
        CombatStatEventKind.CreatureState => I18N.T("combatStats.event.state", "State"),
        _ => kind.ToString(),
    };

    public static string LocalizeEventText(string text) {
        if (string.IsNullOrEmpty(text))
            return text;

        const string arrow = " → ";
        int idx = text.IndexOf(arrow, StringComparison.Ordinal);
        if (idx > 0) {
            string left = LocalizeKey(text[..idx]);
            string right = LocalizeResourcePhrase(text[(idx + arrow.Length)..]);
            return $"{left}{arrow}{right}";
        }

        string localized = LocalizeKey(text);
        return localized == text ? LocalizeResourcePhrase(text) : localized;
    }

    private static string LocalizeResourcePhrase(string text) {
        if (text.EndsWith(" block", StringComparison.OrdinalIgnoreCase)) {
            string prefix = text[..^6].TrimEnd();
            return $"{prefix} {I18N.T("combatStats.event.block", "Block")}";
        }

        if (text.EndsWith(" energy", StringComparison.OrdinalIgnoreCase)) {
            string prefix = text[..^7].TrimEnd();
            return $"{prefix} {I18N.T("combatStats.event.energy", "Energy")}";
        }

        return text;
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

        var relic = ModelDb.AllRelics.FirstOrDefault(r => ((AbstractModel)r).Id.Entry == entry);
        if (relic != null)
            return ResolveRelicName(relic);

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
