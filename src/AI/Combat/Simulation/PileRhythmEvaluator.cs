using System;

namespace KitLib.AI.Combat.Simulation;

/// <summary>Deck-order value for mid-turn and post-draw scoring.</summary>
public static class PileRhythmEvaluator {
    public static int DrawPileOutlook(CombatState state) {
        int deck = state.DrawPile.Count + state.DiscardPile.Count;
        if (deck <= 0) return 0;

        int horizon = Math.Min(10, deck);
        int damage = DrawPlanner.ExpectedDrawnDamage(
            state, horizon, state.MaxEnergy, ThreatModel.FocusVulnerableStacks(state));
        int block = DrawPlanner.ExpectedDrawnBlock(state, horizon, state.MaxEnergy);
        int pollution = DeckPollutionEvaluator.JunkCount(state) * 2;

        int outlook = damage / 2 + block / 3 - pollution;
        if (DrawPlanner.WillReshuffle(state, RelicCombatRules.PlannedHandDraw(state)))
            outlook -= DeckPollutionEvaluator.ImmediatePollutionCost(state) / 4;

        return outlook;
    }

    public static int RemainingTurnPotential(CombatState state) {
        int hand = DeckPollutionEvaluator.ExpectedPlayableDamage(state)
            + DeckPollutionEvaluator.ExpectedPlayableBlock(state) / 2;
        int pile = DrawPileOutlook(state) / 2;
        int energyBonus = state.Energy * 3;
        return hand + pile + energyBonus;
    }
}
