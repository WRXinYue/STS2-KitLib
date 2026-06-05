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

    public static int EffectiveBlock(int block, IReadOnlyList<PlayerCombatModifier> modifiers) {
        if (block <= 0)
            return 0;

        var scaled = (float)block;
        foreach (var mod in modifiers)
            scaled *= mod.BlockMultiplier;
        return System.Math.Max(0, (int)System.Math.Round(scaled));
    }

    public static bool CanAfford(CombatHandCard card, CombatState state) =>
        card.CanPlay && EffectiveCost(card, state.Modifiers) <= state.Energy;
}
