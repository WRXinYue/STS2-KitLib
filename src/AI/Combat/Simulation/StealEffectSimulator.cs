using System;
using System.Collections.Generic;
using System.Linq;

namespace KitLib.AI.Combat.Simulation;

internal static class StealEffectSimulator {
    public static (List<CombatPileCard> draw, List<CombatPileCard> discard) Apply(
        IReadOnlyList<CombatPileCard> draw,
        IReadOnlyList<CombatPileCard> discard) {
        var drawList = draw.ToList();
        var discardList = discard.ToList();
        var pool = drawList.Concat(discardList).Where(c => !c.IsStatus).ToList();
        if (pool.Count == 0)
            return (drawList, discardList);

        var victim = pool.OrderByDescending(StealScore).First();
        drawList = CombatPileSimulator.RemoveFromPile(drawList, victim);
        if (drawList.Count == draw.Count)
            discardList = CombatPileSimulator.RemoveFromPile(discardList, victim);
        return (drawList, discardList);
    }

    static int StealScore(CombatPileCard card) {
        var id = card.Id.ToUpperInvariant();
        if (id.Contains("RARE", StringComparison.Ordinal) && !id.Contains("ANCIENT", StringComparison.Ordinal))
            return 90;
        if (id.Contains("UNCOMMON", StringComparison.Ordinal))
            return 70;
        if (id.Contains("ANCIENT", StringComparison.Ordinal))
            return 10;
        if (id.Contains("STRIKE", StringComparison.Ordinal) || id.Contains("DEFEND", StringComparison.Ordinal))
            return 20;
        return 50 + card.Damage * 2 + card.Block + Math.Min(6, card.Cost);
    }
}
