using System;
using System.Collections.Generic;
using System.Linq;

namespace KitLib.AI.Combat.Simulation;

public static class DrawPlanner {
    public const int DefaultPeekCount = 3;

    public static IReadOnlyList<CombatPileCard> PeekTop(CombatState state, int count) {
        if (count <= 0) return [];

        var draw = state.DrawPile.ToList();
        var discard = state.DiscardPile.ToList();
        var counter = state.ShuffleRngCounter;
        var peeked = new List<CombatPileCard>();

        while (peeked.Count < count) {
            (draw, discard, counter) = CombatPileSimulator.ReshuffleIfNeeded(
                draw, discard, state.ShuffleRngSeed, counter);
            if (draw.Count == 0)
                break;

            peeked.Add(draw[0]);
            draw.RemoveAt(0);
        }

        return peeked;
    }

    public static bool WillReshuffle(CombatState state, int drawsNeeded) {
        if (drawsNeeded <= 0) return false;
        if (state.DrawPile.Count >= drawsNeeded) return false;
        return state.DiscardPile.Count > 0;
    }

    public static string FormatPeekSummary(CombatState state, int count = DefaultPeekCount) {
        var cards = PeekTop(state, count);
        if (cards.Count == 0)
            return "NEXT=-";

        var parts = cards.Select(c => $"{ShortName(c.Name)}+{c.Cost}");
        return $"NEXT={string.Join(',', parts)}";
    }

    public static int ExpectedDrawnDamage(CombatState state, int draws, int energy, int vulnerableOnFocus = 0) {
        int total = 0;
        foreach (var card in PeekTop(state, draws)) {
            if (!string.Equals(card.CardType, "Attack", StringComparison.OrdinalIgnoreCase)
                && card.Damage <= 0)
                continue;
            if (CombatDamageCalc.PlanningCost(card, state.Modifiers, energy) > energy) continue;
            total += CombatDamageCalc.OutgoingDamage(card, state.Modifiers, vulnerableOnFocus);
        }

        return total;
    }

    public static int ExpectedDrawnBlock(CombatState state, int draws, int energy) {
        int total = 0;
        foreach (var card in PeekTop(state, draws)) {
            if (!string.Equals(card.CardType, "Skill", StringComparison.OrdinalIgnoreCase)
                || card.Block <= 0)
                continue;

            if (CombatDamageCalc.PlanningCost(card, state.Modifiers, energy) > energy) continue;
            total += CombatDamageCalc.OutgoingBlock(card, state.Modifiers);
        }

        return total;
    }

    static string ShortName(string name) {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var idx = name.IndexOf(' ');
        return idx > 0 ? name[..idx] : name;
    }
}
