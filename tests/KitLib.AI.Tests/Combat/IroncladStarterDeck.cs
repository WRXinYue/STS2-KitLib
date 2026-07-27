using System.Collections.Generic;
using KitLib.AI.Combat.Simulation;

namespace KitLib.AI.Tests.Combat;

/// <summary>Ironclad starter deck for offline fight sandboxes (10 cards).</summary>
internal static class IroncladStarterDeck {
    public static IReadOnlyList<string> FullDeckIds { get; } = [
        OfficialIroncladCards.Strike,
        OfficialIroncladCards.Strike,
        OfficialIroncladCards.Strike,
        OfficialIroncladCards.Strike,
        OfficialIroncladCards.Strike,
        OfficialIroncladCards.Defend,
        OfficialIroncladCards.Defend,
        OfficialIroncladCards.Defend,
        OfficialIroncladCards.Defend,
        OfficialIroncladCards.Bash,
    ];

    public static List<CombatPileCard> BuildPile() {
        var pile = new List<CombatPileCard>(FullDeckIds.Count);
        foreach (var id in FullDeckIds) {
            if (!SandboxCardCatalog.TryGet(id, out var profile))
                continue;

            pile.Add(new CombatPileCard(
                id,
                id,
                profile.CanonicalCost,
                profile.Damage ?? 0,
                profile.Block ?? 0,
                profile.CardType,
                false,
                false,
                false));
        }

        return pile;
    }

    /// <summary>Split a 10-card starter into opening hand + draw pile (order-stable for tests).</summary>
    public static (List<CombatHandCard> Hand, List<CombatPileCard> Draw) SplitOpening(
        IReadOnlyList<string> handIds) {
        var hand = CombatScenarioFactory.Hand(handIds.ToArray());
        var draw = BuildPile();
        foreach (var id in handIds) {
            int idx = draw.FindIndex(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                throw new InvalidOperationException($"Hand card '{id}' is not in the starter deck.");

            draw.RemoveAt(idx);
        }

        return (hand, draw);
    }
}
