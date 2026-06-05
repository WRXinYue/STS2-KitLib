using System;
using System.Collections.Generic;

namespace DevMode.AI.Combat.Simulation;

internal static class CombatCardCost {
    public static int EffectiveCost(CombatHandCard card, IReadOnlyList<PlayerCombatModifier> modifiers) {
        int cost = card.Cost;
        foreach (var mod in modifiers) {
            if (card.IsAttack)
                cost += mod.AttackCostPenalty;
            if (card.IsSkill)
                cost += mod.SkillCostPenalty;
        }

        return cost;
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
        card.CanPlay && EffectiveCost(card, state.Modifiers) <= state.Energy;

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
