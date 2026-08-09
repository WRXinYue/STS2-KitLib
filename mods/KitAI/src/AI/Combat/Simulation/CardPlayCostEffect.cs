using System;

namespace KitLib.AI.Combat.Simulation;

/// <summary>On-play cost waivers: Unrelenting / Pounce / Synthesis cycle.</summary>
internal static class CardPlayCostEffect {
    public static NextPlayCostWaive? GrantOnPlay(string cardId) {
        if (string.IsNullOrWhiteSpace(cardId))
            return null;

        if (cardId.Equals("UNRELENTING", StringComparison.OrdinalIgnoreCase))
            return NextPlayCostWaive.Attack;
        if (cardId.Equals("POUNCE", StringComparison.OrdinalIgnoreCase))
            return NextPlayCostWaive.Skill;
        if (cardId.Equals("SYNTHESIS", StringComparison.OrdinalIgnoreCase))
            return NextPlayCostWaive.Power;

        return null;
    }

    public static bool MatchesWaive(CombatHandCard card, NextPlayCostWaive waive) =>
        waive != NextPlayCostWaive.None && ConsumesWaive(card, waive);

    public static bool ConsumesWaive(CombatHandCard card, NextPlayCostWaive waive) =>
        waive switch {
            NextPlayCostWaive.Attack => card.IsAttack,
            NextPlayCostWaive.Skill => card.IsSkill,
            NextPlayCostWaive.Power => IsPowerCard(card),
            _ => false,
        };

    static bool IsPowerCard(CombatHandCard card) =>
        string.Equals(card.CardType, "Power", StringComparison.OrdinalIgnoreCase);
}
