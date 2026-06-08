using System;
using System.Text.RegularExpressions;

namespace KitLib.AI.Knowledge;

/// <summary>Last-resort English description fallback when <see cref="OfficialMechanicProbe"/> finds no structural signal.</summary>
internal static class MechanicTextAnalyzer {
    static readonly Regex BbCode = new(@"\[[^\]]*\]", RegexOptions.Compiled);

    public static CardMechanicFlags AnalyzeCardTextFallback(string? description, string? typeName) {
        var flags = CardMechanicFlags.None;
        var text = Normalize(description);
        var type = Normalize(typeName);

        if (ContainsAny(text, "TRANSFORM", "CHANGE ALL", "INTO GIANT ROCK"))
            flags |= CardMechanicFlags.TransformsCards;

        if (ContainsAny(text, "ATTACK") && ContainsAny(text, "HAND"))
            flags |= CardMechanicFlags.TransformsHandAttacks;

        if (type.Contains("TRANSFORM", StringComparison.Ordinal))
            flags |= CardMechanicFlags.TransformsCards;

        if (ContainsAny(text, "DRAW") && !ContainsAny(text, "REDRAW"))
            flags |= CardMechanicFlags.HasDraw;
        if (ContainsAny(text, "DISCARD"))
            flags |= CardMechanicFlags.HasDiscard;
        if (ContainsAny(text, "SCRY"))
            flags |= CardMechanicFlags.HasScry;
        if (ContainsAny(text, "HEAL") && !ContainsAny(text, "MAX HP"))
            flags |= CardMechanicFlags.HasHeal;
        if (ContainsAny(text, "SUMMON", "MINION"))
            flags |= CardMechanicFlags.HasSummon;
        if (ContainsAny(text, "FORGE"))
            flags |= CardMechanicFlags.HasForge;
        if (ContainsAny(text, " STAR", " STARS"))
            flags |= CardMechanicFlags.HasStarCost;
        if (ContainsAny(text, "ALL ENEMY", "ALL ENEMIES"))
            flags |= CardMechanicFlags.Aoe;

        if (ContainsAny(text, "INTO YOUR DECK", "ADD TO YOUR DECK", "SHUFFLE")
            && ContainsAny(text, "CARD", "RANDOM", "ATTACK"))
            flags |= CardMechanicFlags.AddsCardsToDeck;

        return flags;
    }

    public static RelicMechanicFlags AnalyzeRelicTextFallback(string? description) {
        var flags = RelicMechanicFlags.None;
        var text = Normalize(description);

        if (ContainsAny(text, "RARE CARD") && ContainsAny(text, "CHOOSE", "ADD"))
            flags |= RelicMechanicFlags.OffersRarePick;

        if (ContainsAny(text, "CHOOSE") && ContainsAny(text, " CARD"))
            flags |= RelicMechanicFlags.OffersCardPick;

        if (ContainsAny(text, "INJURY", "CURSE"))
            flags |= RelicMechanicFlags.AddsCurseOrInjury;

        if (ContainsAny(text, "MAX HP"))
            flags |= RelicMechanicFlags.AddsMaxHp;

        if (ContainsAny(text, "GOLD"))
            flags |= RelicMechanicFlags.GrantsGold;

        if (ContainsAny(text, "POTION"))
            flags |= RelicMechanicFlags.GrantsPotion;

        if (ContainsAny(text, "REMOVE A CARD", "REMOVE CARD"))
            flags |= RelicMechanicFlags.RemovesCard;

        if (ContainsAny(text, "TRANSFORM"))
            flags |= RelicMechanicFlags.TransformsCards;

        if (ContainsAny(text, "STRENGTH", "DEXTERITY", "FORGE"))
            flags |= RelicMechanicFlags.CombatScaling;

        return flags;
    }

    static string Normalize(string? raw) {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        return BbCode.Replace(raw, " ").ToUpperInvariant();
    }

    static bool ContainsAny(string haystack, params string[] needles) {
        foreach (var needle in needles) {
            if (haystack.Contains(needle, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
