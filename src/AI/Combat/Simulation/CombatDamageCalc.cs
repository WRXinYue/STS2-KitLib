using System;
using System.Collections.Generic;

namespace KitLib.AI.Combat.Simulation;

internal static class CombatDamageCalc {
    public const float ConfusedDrawCostEv = 1.5f;

    public static int OutgoingDamage(int baseDamage, IReadOnlyList<PlayerCombatModifier> modifiers, int vulnerableOnTarget = 0) {
        if (baseDamage <= 0)
            return 0;

        int flat = 0;
        float mult = 1f;
        foreach (var mod in modifiers) {
            flat += mod.AttackDamageFlat;
            mult *= mod.AttackDamageMultiplier;
        }

        var scaled = (int)Math.Round((baseDamage + flat) * mult);
        if (vulnerableOnTarget > 0)
            scaled = (int)Math.Round(scaled * 1.5f);
        return Math.Max(0, scaled);
    }

    public static int OutgoingDamage(CombatHandCard card, CombatState state, int vulnerableOnTarget = 0, int skillsInHand = 0) {
        if (!card.IsAttack || card.Damage <= 0)
            return 0;
        var perHit = OutgoingDamage(card.Damage, state.Modifiers, vulnerableOnTarget);
        return perHit * Math.Max(0, CombatCardStats.ResolveEffectiveHitCount(card, state, skillsInHand));
    }

    public static int OutgoingDamage(CombatPileCard card, IReadOnlyList<PlayerCombatModifier> modifiers, int vulnerableOnTarget = 0) {
        if (card.Damage <= 0)
            return 0;
        bool isAttack = string.Equals(card.CardType, "Attack", StringComparison.OrdinalIgnoreCase)
            || card.Damage > 0;
        if (!isAttack)
            return 0;
        return OutgoingDamage(card.Damage, modifiers, vulnerableOnTarget);
    }

    public static int OutgoingBlock(int baseBlock, IReadOnlyList<PlayerCombatModifier> modifiers) {
        if (baseBlock <= 0)
            return 0;

        int flat = 0;
        float mult = 1f;
        foreach (var mod in modifiers) {
            flat += mod.BlockFlat;
            mult *= mod.BlockMultiplier;
        }

        return Math.Max(0, (int)Math.Round((baseBlock + flat) * mult));
    }

    public static int OutgoingBlock(CombatHandCard card, CombatState state) =>
        OutgoingBlock(card.Block, state.Modifiers);

    public static int OutgoingBlock(CombatPileCard card, IReadOnlyList<PlayerCombatModifier> modifiers) =>
        OutgoingBlock(card.Block, modifiers);

    public static bool HasConfusedDrawCostEv(IReadOnlyList<PlayerCombatModifier> modifiers) {
        foreach (var mod in modifiers) {
            if (mod.ConfusedDrawCostEv)
                return true;
        }

        return false;
    }

    public static int PlanningCost(CombatPileCard card, IReadOnlyList<PlayerCombatModifier> modifiers, int energy) {
        int cost = CombatCardCost.EffectiveCost(card, modifiers);
        if (!HasConfusedDrawCostEv(modifiers))
            return cost;

        int evCost = (int)Math.Ceiling(ConfusedDrawCostEv);
        return Math.Max(cost, evCost);
    }
}
