using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Knowledge;

/// <summary>
/// Locale-independent mechanic discovery from official <see cref="CardModel"/> /
/// <see cref="RelicModel"/> structure (keywords, dynamic vars, type graph, loc keys).
/// </summary>
internal static class OfficialMechanicProbe {
    const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const int MaxGraphDepth = 2;

    static readonly CardMechanicFlags CardStructuralMask =
        CardMechanicFlags.TransformsCards
        | CardMechanicFlags.TransformsHandAttacks
        | CardMechanicFlags.HasDraw
        | CardMechanicFlags.HasDiscard
        | CardMechanicFlags.HasScry
        | CardMechanicFlags.HasHeal
        | CardMechanicFlags.HasSummon
        | CardMechanicFlags.HasForge
        | CardMechanicFlags.HasStarCost
        | CardMechanicFlags.HasExhaustFromHand;

    public static CardMechanicFlags ProbeCard(CardModel card) {
        var flags = CardMechanicFlags.None;

        foreach (var kw in card.Keywords)
            flags |= FlagsFromKeyword(kw);

        foreach (var key in CardEditActions.GetDynamicVarKeys(card))
            flags |= FlagsFromDynamicVar(key);

        if (CardEditActions.GetDamage(card) > 0)
            flags |= CardMechanicFlags.HasDamage;
        if (CardEditActions.GetBlock(card) > 0)
            flags |= CardMechanicFlags.HasBlock;

        if (card.Type == CardType.Attack)
            flags |= CardMechanicFlags.HasDamage;

        if (card.TargetType.ToString().Contains("AllEnemy", StringComparison.OrdinalIgnoreCase))
            flags |= CardMechanicFlags.Aoe;

        flags |= AnalyzeTokenBlob(CollectTypeTokenBlob(card.GetType()));
        flags |= AnalyzeTokenBlob(CollectInstanceTokenBlob(card));
        flags |= AnalyzeTokenBlob(card.Id.Entry ?? "");
        flags |= AnalyzeCardLocKeys(CollectLocKeys(card));

        if (string.Equals(card.Id.Entry, "PRIMAL_FORCE", StringComparison.OrdinalIgnoreCase)
            || card.GetType().Name.Contains("PrimalForce", StringComparison.Ordinal))
            flags |= CardMechanicFlags.TransformsHandAttacks;

        if (string.Equals(card.Id.Entry, "PILLAGE", StringComparison.OrdinalIgnoreCase)
            || card.GetType().Name.Contains("Pillage", StringComparison.Ordinal))
            flags |= CardMechanicFlags.AddsCardsToDeck;

        if (ProbesExhaustFromHand(card))
            flags |= CardMechanicFlags.HasExhaustFromHand;

        return flags;
    }

    /// <summary>Exhaust hover on a card that does not itself exhaust (Burning Pact, True Grit, Brand).</summary>
    static bool ProbesExhaustFromHand(CardModel card) {
        if (card.Keywords.Contains(CardKeyword.Exhaust))
            return false;

        try {
            var prop = typeof(CardModel).GetProperty("ExtraHoverTips", MemberFlags);
            if (prop?.GetValue(card) is not IEnumerable tips)
                return false;

            foreach (var tip in tips) {
                if (tip == null) continue;
                var blob = $"{tip.GetType().Name} {tip}";
                if (blob.Contains("Exhaust", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { /* ignore */ }

        return false;
    }

    public static RelicMechanicFlags ProbeRelic(RelicModel relic) {
        var flags = RelicMechanicFlags.None;

        flags |= AnalyzeRelicTokenBlob(CollectTypeTokenBlob(relic.GetType()));
        flags |= AnalyzeRelicTokenBlob(CollectInstanceTokenBlob(relic));
        flags |= AnalyzeRelicLocKeys(CollectLocKeys(relic));

        foreach (var key in ReadDynamicVarKeys(relic))
            flags |= RelicFlagsFromDynamicVar(key);

        return flags;
    }

    public static bool NeedsCardTextFallback(CardMechanicFlags flags) =>
        (flags & CardStructuralMask) == CardMechanicFlags.None;

    public static bool NeedsRelicTextFallback(RelicMechanicFlags flags) =>
        flags == RelicMechanicFlags.None;

    static string CollectTypeTokenBlob(Type type) {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var t = type; t != null && t != typeof(object); t = t.BaseType) {
            if (!string.IsNullOrWhiteSpace(t.Name))
                tokens.Add(t.Name);
            if (!string.IsNullOrWhiteSpace(t.Namespace))
                tokens.Add(t.Namespace);
        }
        return string.Join(' ', tokens);
    }

    static string CollectInstanceTokenBlob(object root) {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ScanObjectGraph(root, tokens, depth: 0, maxDepth: 1);
        return string.Join(' ', tokens);
    }

    static void ScanObjectGraph(object? root, HashSet<string> tokens, int depth, int maxDepth) {
        if (root == null || depth > maxDepth)
            return;

        var type = root.GetType();
        if (type.IsPrimitive || type.IsEnum)
            return;

        if (root is string text) {
            if (!string.IsNullOrWhiteSpace(text))
                tokens.Add(text);
            return;
        }

        if (root is ModelId modelId && !string.IsNullOrWhiteSpace(modelId.Entry)) {
            tokens.Add(modelId.Entry);
            return;
        }

        if (root is CardModel card && !string.IsNullOrWhiteSpace(card.Id.Entry)) {
            tokens.Add(card.Id.Entry);
            return;
        }

        if (root is AbstractModel model && !string.IsNullOrWhiteSpace(model.Id.Entry)) {
            tokens.Add(model.Id.Entry);
            return;
        }

        if (depth >= maxDepth)
            return;

        foreach (var prop in type.GetProperties(MemberFlags)) {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                continue;
            object? value;
            try { value = prop.GetValue(root); }
            catch { continue; }
            CollectMemberValue(value, prop.Name, tokens, depth + 1, maxDepth);
        }

        foreach (var field in type.GetFields(MemberFlags)) {
            object? value;
            try { value = field.GetValue(root); }
            catch { continue; }
            CollectMemberValue(value, field.Name, tokens, depth + 1, maxDepth);
        }
    }

    static void CollectMemberValue(object? value, string memberName, HashSet<string> tokens, int depth, int maxDepth) {
        if (!string.IsNullOrWhiteSpace(memberName))
            tokens.Add(memberName);

        if (value is string s) {
            if (!string.IsNullOrWhiteSpace(s))
                tokens.Add(s);
            return;
        }

        if (value is IEnumerable enumerable and not string) {
            foreach (var item in enumerable)
                ScanObjectGraph(item, tokens, depth, maxDepth);
            return;
        }

        ScanObjectGraph(value, tokens, depth, maxDepth);
    }

    static IEnumerable<string> CollectLocKeys(object model) {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var loc in EnumerateLocStrings(model, depth: 0))
            AddLocKey(keys, loc);
        return keys;
    }

    static IEnumerable<LocString> EnumerateLocStrings(object? root, int depth) {
        if (root == null || depth > MaxGraphDepth)
            yield break;

        if (root is LocString loc) {
            yield return loc;
            yield break;
        }

        var type = root.GetType();
        if (type.IsPrimitive || type.IsEnum || root is string)
            yield break;

        foreach (var prop in type.GetProperties(MemberFlags)) {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                continue;
            object? value;
            try { value = prop.GetValue(root); }
            catch { continue; }

            if (value is LocString locProp) {
                yield return locProp;
                continue;
            }

            foreach (var nested in EnumerateLocStrings(value, depth + 1))
                yield return nested;
        }

        foreach (var field in type.GetFields(MemberFlags)) {
            object? value;
            try { value = field.GetValue(root); }
            catch { continue; }

            if (value is LocString locField) {
                yield return locField;
                continue;
            }

            foreach (var nested in EnumerateLocStrings(value, depth + 1))
                yield return nested;
        }
    }

    static void AddLocKey(HashSet<string> keys, LocString loc) {
        var key = TryReadLocKey(loc);
        if (!string.IsNullOrWhiteSpace(key))
            keys.Add(key);

        var id = TryReadModelIdFromLoc(loc);
        if (!string.IsNullOrWhiteSpace(id))
            keys.Add(id);
    }

    static string? TryReadLocKey(LocString loc) {
        foreach (var name in new[] { "Key", "Path", "LocalizationKey", "LocKey", "_key", "_path" }) {
            try {
                var prop = loc.GetType().GetProperty(name, MemberFlags);
                if (prop?.GetValue(loc) is string s && !string.IsNullOrWhiteSpace(s))
                    return s;
                var field = loc.GetType().GetField(name, MemberFlags);
                if (field?.GetValue(loc) is string f && !string.IsNullOrWhiteSpace(f))
                    return f;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    static string? TryReadModelIdFromLoc(LocString loc) {
        foreach (var name in new[] { "ModelId", "Id", "_modelId" }) {
            try {
                var prop = loc.GetType().GetProperty(name, MemberFlags);
                if (prop?.GetValue(loc) is ModelId modelId && !string.IsNullOrWhiteSpace(modelId.Entry))
                    return modelId.Entry;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    static IEnumerable<string> ReadDynamicVarKeys(RelicModel relic) {
        var keys = new List<string>();
        try {
            var prop = relic.GetType().GetProperty("DynamicVars", MemberFlags);
            if (prop?.GetValue(relic) is IDictionary dict) {
                foreach (var key in dict.Keys) {
                    if (key is string s && !string.IsNullOrWhiteSpace(s))
                        keys.Add(s);
                }
            }
        }
        catch { /* ignore */ }
        return keys;
    }

    public static CardMechanicFlags FlagsFromKeyword(CardKeyword keyword) => keyword switch {
        CardKeyword.Exhaust => CardMechanicFlags.Exhaust,
        CardKeyword.Retain => CardMechanicFlags.Retain,
        CardKeyword.Ethereal => CardMechanicFlags.Ethereal,
        _ => FlagsFromKeywordName(keyword.ToString()),
    };

    public static CardMechanicFlags FlagsFromKeywordName(string name) {
        var upper = name.ToUpperInvariant();
        var flags = CardMechanicFlags.None;
        if (upper.Contains("EXHAUST", StringComparison.Ordinal)) flags |= CardMechanicFlags.Exhaust;
        if (upper.Contains("RETAIN", StringComparison.Ordinal)) flags |= CardMechanicFlags.Retain;
        if (upper.Contains("ETHEREAL", StringComparison.Ordinal)) flags |= CardMechanicFlags.Ethereal;
        if (upper.Contains("FORGE", StringComparison.Ordinal)) flags |= CardMechanicFlags.HasForge;
        if (upper.Contains("STAR", StringComparison.Ordinal)) flags |= CardMechanicFlags.HasStarCost;
        if (upper.Contains("SUMMON", StringComparison.Ordinal)) flags |= CardMechanicFlags.HasSummon;
        return flags;
    }

    public static CardMechanicFlags FlagsFromDynamicVar(string key) {
        var upper = key.ToUpperInvariant();
        if (upper.Contains("DRAW", StringComparison.Ordinal)) return CardMechanicFlags.HasDraw;
        if (string.Equals(upper, "CARDS", StringComparison.Ordinal)) return CardMechanicFlags.HasDraw;
        if (upper.Contains("DISCARD", StringComparison.Ordinal)) return CardMechanicFlags.HasDiscard;
        if (upper.Contains("SCRY", StringComparison.Ordinal)) return CardMechanicFlags.HasScry;
        if (upper.Contains("HEAL", StringComparison.Ordinal)) return CardMechanicFlags.HasHeal;
        if (upper.Contains("SUMMON", StringComparison.Ordinal)) return CardMechanicFlags.HasSummon;
        if (upper.Contains("FORGE", StringComparison.Ordinal)) return CardMechanicFlags.HasForge;
        if (upper.Contains("STAR", StringComparison.Ordinal)) return CardMechanicFlags.HasStarCost;
        if (upper.Contains("REPEAT", StringComparison.Ordinal)) return CardMechanicFlags.HasDamage;
        if (upper.Contains("VULNERABLE", StringComparison.Ordinal)) return CardMechanicFlags.AppliesVulnerable;
        if (upper.Contains("WEAK", StringComparison.Ordinal)) return CardMechanicFlags.AppliesWeak;
        return CardMechanicFlags.None;
    }

    static RelicMechanicFlags RelicFlagsFromDynamicVar(string key) {
        var upper = key.ToUpperInvariant();
        if (upper.Contains("GOLD", StringComparison.Ordinal)) return RelicMechanicFlags.GrantsGold;
        if (upper.Contains("HEAL", StringComparison.Ordinal)) return RelicMechanicFlags.AddsMaxHp;
        return RelicMechanicFlags.None;
    }

    public static CardMechanicFlags AnalyzeCardLocKeys(IEnumerable<string> locKeys) {
        var flags = CardMechanicFlags.None;
        foreach (var key in locKeys)
            flags |= AnalyzeTokenBlob(key);
        return flags;
    }

    public static RelicMechanicFlags AnalyzeRelicLocKeys(IEnumerable<string> locKeys) {
        var flags = RelicMechanicFlags.None;
        foreach (var key in locKeys)
            flags |= AnalyzeRelicTokenBlob(key);
        return flags;
    }

    public static CardMechanicFlags AnalyzeTokenBlob(string blob) {
        if (string.IsNullOrWhiteSpace(blob))
            return CardMechanicFlags.None;

        var upper = blob.ToUpperInvariant();
        var flags = CardMechanicFlags.None;

        if (ContainsAny(upper, "TRANSFORM", "GIANT_ROCK", "GIANTROCK", "PRIMAL_FORCE", "PRIMALFORCE"))
            flags |= CardMechanicFlags.TransformsCards;

        if (ContainsAny(upper, "PRIMAL_FORCE", "PRIMALFORCE", "HAND")
            && ContainsAny(upper, "ATTACK", "STRIKE"))
            flags |= CardMechanicFlags.TransformsHandAttacks;

        if (ContainsAny(upper, "DRAW") && !ContainsAny(upper, "REDRAW"))
            flags |= CardMechanicFlags.HasDraw;
        if (ContainsAny(upper, "DISCARD"))
            flags |= CardMechanicFlags.HasDiscard;
        if (ContainsAny(upper, "SCRY"))
            flags |= CardMechanicFlags.HasScry;
        if (ContainsAny(upper, "HEAL") && !ContainsAny(upper, "MAXHP", "MAX_HP"))
            flags |= CardMechanicFlags.HasHeal;
        if (ContainsAny(upper, "SUMMON", "MINION"))
            flags |= CardMechanicFlags.HasSummon;
        if (ContainsAny(upper, "FORGE"))
            flags |= CardMechanicFlags.HasForge;
        if (ContainsAny(upper, "STAR"))
            flags |= CardMechanicFlags.HasStarCost;
        if (ContainsAny(upper, "ALLENEMY", "ALL_ENEMY"))
            flags |= CardMechanicFlags.Aoe;

        if (ContainsAny(upper, "PILLAGE", "ADDCARDTODECK", "ADDS_CARD"))
            flags |= CardMechanicFlags.AddsCardsToDeck;

        return flags;
    }

    static RelicMechanicFlags AnalyzeRelicTokenBlob(string blob) {
        if (string.IsNullOrWhiteSpace(blob))
            return RelicMechanicFlags.None;

        var upper = blob.ToUpperInvariant();
        var flags = RelicMechanicFlags.None;

        if (ContainsAny(upper, "HEFTY_TABLET", "HEFTYTABLET"))
            flags |= RelicMechanicFlags.OffersRarePick | RelicMechanicFlags.AddsCurseOrInjury;

        if (ContainsAny(upper, "RARE") && ContainsAny(upper, "CARD", "DECK"))
            flags |= RelicMechanicFlags.OffersRarePick;

        if (ContainsAny(upper, "CHOOSE") && ContainsAny(upper, "CARD"))
            flags |= RelicMechanicFlags.OffersCardPick;

        if (ContainsAny(upper, "INJURY", "CURSE", "WOUND", "ASCENDERSBANE"))
            flags |= RelicMechanicFlags.AddsCurseOrInjury;

        if (ContainsAny(upper, "MAXHP", "MAX_HP"))
            flags |= RelicMechanicFlags.AddsMaxHp;

        if (ContainsAny(upper, "GOLD"))
            flags |= RelicMechanicFlags.GrantsGold;

        if (ContainsAny(upper, "POTION"))
            flags |= RelicMechanicFlags.GrantsPotion;

        if (ContainsAny(upper, "REMOVE") && ContainsAny(upper, "CARD"))
            flags |= RelicMechanicFlags.RemovesCard;

        if (ContainsAny(upper, "TRANSFORM"))
            flags |= RelicMechanicFlags.TransformsCards;

        if (ContainsAny(upper, "STRENGTH", "DEXTERITY", "FORGE", "PRIMAL_FORCE"))
            flags |= RelicMechanicFlags.CombatScaling;

        return flags;
    }

    static bool ContainsAny(string haystack, params string[] needles) {
        foreach (var needle in needles) {
            if (haystack.Contains(needle, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
