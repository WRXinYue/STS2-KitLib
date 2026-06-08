using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Knowledge;

/// <summary>Heuristic tag inference from card ids and keywords (no per-card hardcoding).</summary>
public static class CardTagRules {
    static readonly (string Prefix, AiTag Tag)[] IdPrefixTags = [
        ("STRIKE", AiTag.Attack),
        ("DEFEND", AiTag.Block),
        ("BASH", AiTag.Attack),
        ("FLEX", AiTag.Scaling),
        ("BATTLE_TRANCE", AiTag.Draw),
        ("OFFERING", AiTag.Exhaust),
        ("CORRUPTION", AiTag.Exhaust),
        ("DARK_EMBRACE", AiTag.Draw),
        ("FEEL_NO_PAIN", AiTag.Exhaust),
        ("BLADE_DANCE", AiTag.Attack),
        ("CLOAK_AND_DAGGER", AiTag.Block),
        ("ADRENALINE", AiTag.Energy),
        ("CLEAVE", AiTag.Aoe),
        ("WHIRLWIND", AiTag.Aoe),
        ("CLAW", AiTag.Scaling),
        ("DEFRAGMENT", AiTag.Scaling),
        ("BARRICADE", AiTag.Block),
        ("IMPERVIOUS", AiTag.Block),
    ];

    public static IReadOnlyList<AiTag> InferTags(CardModel card) {
        var tags = new HashSet<AiTag>();
        var id = card.Id.Entry ?? "";

        foreach (var (prefix, tag) in IdPrefixTags) {
            if (id.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                tags.Add(tag);
        }

        if (card.Type == CardType.Attack)
            tags.Add(AiTag.Attack);
        if (card.Type == CardType.Skill && CardEditActions.GetBlock(card) > 0)
            tags.Add(AiTag.Block);

        foreach (var kw in card.Keywords) {
            switch (kw) {
                case CardKeyword.Exhaust:
                    tags.Add(AiTag.Exhaust);
                    break;
                case CardKeyword.Retain:
                    tags.Add(AiTag.Setup);
                    break;
            }
        }

        if (CardEditActions.GetDamage(card) >= 10)
            tags.Add(AiTag.Scaling);

        if (card.TargetType.ToString().Contains("AllEnemy", StringComparison.OrdinalIgnoreCase))
            tags.Add(AiTag.Aoe);

        if (CardEditActions.GetDamage(card) > 0 && card.EnergyCost.Canonical <= 1)
            tags.Add(AiTag.Attack);

        MergeIndexedTags(id, tags);
        return tags.Count > 0 ? [.. tags] : [AiTag.Utility];
    }

    public static IReadOnlyList<AiTag> InferTagsFromSnapshot(string? id, string? cardType, JsonArray? keywords) {
        var tags = new HashSet<AiTag>();
        id ??= "";

        foreach (var (prefix, tag) in IdPrefixTags) {
            if (id.Contains(prefix, StringComparison.OrdinalIgnoreCase))
                tags.Add(tag);
        }

        if (cardType?.Contains("Attack", StringComparison.OrdinalIgnoreCase) == true)
            tags.Add(AiTag.Attack);

        if (keywords != null) {
            foreach (var node in keywords) {
                var kw = node?.GetValue<string>() ?? "";
                if (kw.Contains("Exhaust", StringComparison.OrdinalIgnoreCase))
                    tags.Add(AiTag.Exhaust);
                if (kw.Contains("Retain", StringComparison.OrdinalIgnoreCase))
                    tags.Add(AiTag.Setup);
            }
        }

        MergeIndexedTags(id, tags);
        return tags.Count > 0 ? [.. tags] : [AiTag.Utility];
    }

    static void MergeIndexedTags(string id, HashSet<AiTag> tags) {
        if (!CardMechanicIndex.TryGet(id, out var profile))
            return;
        foreach (var tag in profile.DerivedTags)
            tags.Add(tag);
    }
}
