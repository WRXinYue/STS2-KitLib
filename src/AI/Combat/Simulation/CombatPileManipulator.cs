using System;
using System.Collections.Generic;
using System.Linq;

namespace KitLib.AI.Combat.Simulation;

internal static class CombatPileManipulator {
    static readonly HashSet<string> PutDiscardOnDrawTop = new(StringComparer.OrdinalIgnoreCase) {
        "HEADBUTT",
        "THINKING_AHEAD",
        "COSMIC_INDIFFERENCE",
        "GLIMMER",
        "SHINING_STRIKE",
        "PHOTON_CUT",
    };

    public static bool PutsDiscardOnDrawTop(string cardId) =>
        PutDiscardOnDrawTop.Contains(cardId);

    public static (List<CombatPileCard> draw, List<CombatPileCard> discard) ApplyOnPlay(
        CombatState state,
        string cardId,
        IReadOnlyList<CombatPileCard> draw,
        IReadOnlyList<CombatPileCard> discard) {
        if (!PutsDiscardOnDrawTop(cardId) || discard.Count == 0)
            return (draw.ToList(), discard.ToList());

        var discardList = discard.ToList();
        var best = PickBestDiscardToTop(state, discardList);
        if (best == null)
            return (draw.ToList(), discardList);

        discardList = CombatPileSimulator.RemoveFromPile(discardList, best);
        var drawList = CombatPileSimulator.AddToTop(draw, best);
        return (drawList, discardList);
    }

    static CombatPileCard? PickBestDiscardToTop(CombatState state, List<CombatPileCard> discard) {
        CombatPileCard? best = null;
        int bestScore = int.MinValue;

        foreach (var card in discard) {
            int score = CombatDiscardPickScorer.TopPickScore(state, card);
            if (score > bestScore) {
                bestScore = score;
                best = card;
            }
        }

        return best;
    }
}
