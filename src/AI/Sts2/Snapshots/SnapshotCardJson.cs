using System.Collections.Generic;
using System.Text.Json.Nodes;
using DevMode.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace DevMode.AI.Sts2.Snapshots;

internal static class SnapshotCardJson {
    public static JsonObject FromCard(CardModel card, int index = -1) {
        var obj = new JsonObject {
            ["id"] = card.Id.Entry ?? "",
            ["name"] = card.Title,
            ["cost"] = SnapshotEnergyCost(card),
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

        var damage = CardEditActions.GetDamage(card);
        if (damage.HasValue)
            obj["damage"] = damage.Value;

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

    static int SnapshotEnergyCost(CardModel card) {
        try {
            return card.EnergyCost.GetWithModifiers(CostModifiers.All);
        }
        catch {
            return card.EnergyCost.Canonical;
        }
    }
}
