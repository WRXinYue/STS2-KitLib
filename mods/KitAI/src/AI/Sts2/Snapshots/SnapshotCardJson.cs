using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.Actions;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Sts2.Snapshots;

internal static class SnapshotCardJson {
    public static JsonObject FromCard(CardModel card, int index = -1) {
        var obj = new JsonObject {
            ["id"] = card.Id.Entry ?? "",
            ["name"] = SafeCardTitle(card),
            ["cost"] = ResolveEnergyCost(card),
            ["upgradeLevel"] = card.CurrentUpgradeLevel,
            ["maxUpgradeLevel"] = card.MaxUpgradeLevel,
            ["cardType"] = card.Type.ToString(),
            ["rarity"] = card.Rarity.ToString(),
            ["targetType"] = card.TargetType.ToString(),
        };

        if (index >= 0)
            obj["index"] = index;

        var keywords = new JsonArray();
        foreach (var kw in card.Keywords)
            keywords.Add(kw.ToString());
        obj["keywords"] = keywords;
        obj["isStatus"] = CombatJunkCard.IsJunkId(card.Id.Entry, card.Rarity.ToString());
        obj["hasRetain"] = card.Keywords.Contains(CardKeyword.Retain);
        obj["hasExhaust"] = card.Keywords.Contains(CardKeyword.Exhaust);

        var damage = CardEditActions.GetDamage(card);
        if (damage.HasValue)
            obj["damage"] = damage.Value;

        if (CardMechanicIndex.TryGet(card.Id.Entry, out var profile)) {
            if (profile.CostsEnergyX)
                obj["costsX"] = true;
            if (profile.AttackHitCount > 1 && !profile.AttackHitsScaleWithEnergy
                && profile.HitScaleMode == AttackHitScaleMode.None)
                obj["hitCount"] = profile.AttackHitCount;
            if (profile.ReplayCount > 0)
                obj["replayCount"] = profile.ReplayCount;
        }

        var block = CardEditActions.GetBlock(card);
        if (block.HasValue)
            obj["block"] = block.Value;

        try {
            var pool = card.Pool?.Title;
            if (!string.IsNullOrWhiteSpace(pool))
                obj["pool"] = pool;
        }
        catch { }

        return obj;
    }

    static string SafeCardTitle(CardModel card) {
        try {
            var title = card.Title?.ToString();
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }
        catch { }
        return card.Id.Entry ?? "";
    }

    internal static int ResolveEnergyCost(CardModel card) {
        try {
            return card.EnergyCost.GetWithModifiers(CostModifiers.All);
        }
        catch {
            return card.EnergyCost.Canonical;
        }
    }
}
