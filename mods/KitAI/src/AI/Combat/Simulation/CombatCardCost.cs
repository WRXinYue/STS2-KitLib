using System;
using System.Collections.Generic;

namespace KitLib.AI.Combat.Simulation;

internal static class CombatCardCost {
    public static int EffectiveCost(CombatHandCard card, CombatState state) =>
        EffectiveCostWithWaive(card, state.Modifiers, state.NextPlayCostWaive, state.Energy);

    public static int EffectiveCost(CombatHandCard card, IReadOnlyList<PlayerCombatModifier> modifiers) =>
        EffectiveCostWithWaive(card, modifiers, NextPlayCostWaive.None);

    static int EffectiveCostWithWaive(
        CombatHandCard card,
        IReadOnlyList<PlayerCombatModifier> modifiers,
        NextPlayCostWaive waive,
        int? availableEnergy = null) {
        if (CardPlayCostEffect.MatchesWaive(card, waive))
            return 0;

        if (card.Profile.CostsEnergyX)
            return Math.Max(0, availableEnergy ?? 0);

        int cost = card.Cost;
        foreach (var mod in modifiers) {
            if (card.IsAttack)
                cost += mod.AttackCostPenalty;
            if (card.IsSkill)
                cost += mod.SkillCostPenalty;
        }

        return Math.Max(0, cost);
    }

    public static int EffectiveCost(CombatPileCard card, IReadOnlyList<PlayerCombatModifier> modifiers) {
        int cost = card.Cost;
        bool isAttack = string.Equals(card.CardType, "Attack", StringComparison.OrdinalIgnoreCase)
            || card.Damage > 0;
        bool isSkill = string.Equals(card.CardType, "Skill", StringComparison.OrdinalIgnoreCase);

        foreach (var mod in modifiers) {
            if (isAttack)
                cost += mod.AttackCostPenalty;
            if (isSkill)
                cost += mod.SkillCostPenalty;
        }

        return cost;
    }

    public static int EffectiveBlock(int block, IReadOnlyList<PlayerCombatModifier> modifiers) =>
        CombatDamageCalc.OutgoingBlock(block, modifiers);

    public static bool CanAfford(CombatHandCard card, CombatState state) =>
        card.CanPlay && EffectiveCost(card, state) <= state.Energy;

    public static int CountAffordable(CombatState state) {
        int count = 0;
        foreach (var card in state.Hand) {
            if (CanAfford(card, state))
                count++;
        }

        return count;
    }

    public static bool HasAffordablePlay(CombatState state) => CountAffordable(state) > 0;
}
