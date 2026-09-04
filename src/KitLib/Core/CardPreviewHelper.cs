using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace KitLib;

/// <summary>
/// Safe card preview for DevMode browsers. Some event cards (e.g. MadScience) need runtime
/// fields before their description loc strings can format outside TinkerTime.
/// </summary>
public static class CardPreviewHelper {
    public static CardModel GetDisplayModel(CardModel card, bool applyUpgradePreview = false) {
        CardModel model = NeedsRuntimePreviewContext(card)
            ? CloneWithPreviewContext(card)
            : card;

        if (!applyUpgradePreview)
            return model;

        try {
            if (!model.IsUpgradable)
                return model;
            var upgraded = (CardModel)model.MutableClone();
            upgraded.UpgradeInternal();
            return upgraded;
        }
        catch {
            return model;
        }
    }

    public static string GetDescription(CardModel card, bool forUpgradePreview = false) {
        try {
            var model = GetDisplayModel(card, applyUpgradePreview: forUpgradePreview);
            return forUpgradePreview
                ? model.GetDescriptionForUpgradePreview() ?? ""
                : model.GetDescriptionForPile(PileType.None) ?? "";
        }
        catch {
            return "";
        }
    }

    public static string GetSearchDescription(CardModel card) {
        try { return GetDescription(card).StripBbCode(); }
        catch { return ""; }
    }

    private static bool NeedsRuntimePreviewContext(CardModel card) =>
        card is MadScience { TinkerTimeType: CardType.None };

    private static CardModel CloneWithPreviewContext(CardModel card) {
        var clone = (CardModel)card.MutableClone();
        if (clone is MadScience madScience)
            madScience.TinkerTimeType = CardType.Attack;
        return clone;
    }
}
