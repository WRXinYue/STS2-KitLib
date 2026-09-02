using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Game;

internal static class LeanCardJson {
    public static JsonObject FromCard(CardModel card, int index = -1) {
        var obj = new JsonObject {
            ["id"] = card.Id.Entry ?? "",
            ["name"] = SafeCardTitle(card),
            ["cost"] = ResolveEnergyCost(card),
            ["targetType"] = card.TargetType.ToString(),
        };
        if (index >= 0)
            obj["index"] = index;
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
