using System.Collections.Generic;
using System.Linq;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

internal static class CombatPileSimulator {
    public const int BaseHandDrawCount = 5;

    public static (List<CombatHandCard> retained, List<CombatPileCard> discard) DiscardHand(
        IReadOnlyList<CombatHandCard> hand,
        IReadOnlyList<CombatPileCard> discard) {
        var retained = new List<CombatHandCard>();
        var newDiscard = discard.ToList();

        foreach (var card in hand) {
            if (card.HasRetain) {
                retained.Add(card);
                continue;
            }

            newDiscard.Add(HandToPile(card));
        }

        return (retained, newDiscard);
    }

    public static (List<CombatHandCard> hand, List<CombatPileCard> draw, List<CombatPileCard> discard)
        DrawHand(
            IReadOnlyList<CombatHandCard> retainedHand,
            IReadOnlyList<CombatPileCard> draw,
            IReadOnlyList<CombatPileCard> discard,
            int count) {
        var newHand = retainedHand.Select((c, i) => c with { HandIndex = i }).ToList();
        var drawPile = draw.ToList();
        var discardPile = discard.ToList();
        int drawn = 0;

        while (drawn < count) {
            if (drawPile.Count == 0) {
                if (discardPile.Count == 0)
                    break;

                drawPile = discardPile.ToList();
                discardPile.Clear();
            }

            var card = drawPile[0];
            drawPile.RemoveAt(0);
            newHand.Add(PileToHand(card, newHand.Count));
            drawn++;
        }

        return (newHand, drawPile, discardPile);
    }

    public static List<CombatPileCard> InjectStatus(
        IReadOnlyList<CombatPileCard> pile,
        string cardId,
        int count) {
        var result = pile.ToList();
        for (int i = 0; i < count; i++) {
            result.Add(new CombatPileCard(
                cardId,
                cardId,
                0,
                0,
                0,
                "Status",
                IsStatus: true,
                HasRetain: false,
                HasExhaust: false));
        }

        return result;
    }

    static CombatPileCard HandToPile(CombatHandCard card) =>
        new(
            card.Id,
            card.Name,
            card.Cost,
            card.Damage,
            card.Block,
            card.CardType,
            CombatJunkCard.IsJunkId(card.Id),
            card.HasRetain,
            card.HasExhaust);

    static CombatHandCard PileToHand(CombatPileCard card, int index) =>
        new(
            index,
            card.Id,
            card.Name,
            card.Cost,
            card.Damage,
            card.Block,
            card.CardType,
            "",
            CanPlay: !card.IsStatus || card.Damage > 0 || card.Block > 0,
            CardMechanicIndex.InferFromSnapshot(card.ToJson()),
            false,
            card.HasRetain,
            card.HasExhaust);
}
