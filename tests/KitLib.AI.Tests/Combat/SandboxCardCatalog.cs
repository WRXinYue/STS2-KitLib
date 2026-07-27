using System;
using System.Collections.Generic;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Tests.Combat;

/// <summary>
/// Offline card stats aligned with Ironclad starters (same numbers KitLib reads from ModelDb in-game).
/// Used when <see cref="CardMechanicIndex"/> cannot bootstrap outside the game process.
/// </summary>
internal static class SandboxCardCatalog {
    sealed record Spec(
        int Cost,
        int Damage,
        int Block,
        string CardType,
        int AppliedVulnerable,
        int AppliedWeak);

    static readonly Dictionary<string, Spec> ById = new(StringComparer.OrdinalIgnoreCase) {
        ["STRIKE"] = new(1, 6, 0, "Attack", 0, 0),
        ["STRIKE_IRONCLAD"] = new(1, 6, 0, "Attack", 0, 0),
        ["DEFEND"] = new(1, 0, 5, "Skill", 0, 0),
        ["DEFEND_IRONCLAD"] = new(1, 0, 5, "Skill", 0, 0),
        ["BASH"] = new(2, 8, 0, "Attack", 2, 0),
        ["BASH_IRONCLAD"] = new(2, 8, 0, "Attack", 2, 0),
    };

    public static bool TryGet(string cardId, out CardMechanicProfile profile) {
        if (!ById.TryGetValue(cardId, out var spec)) {
            profile = null!;
            return false;
        }

        var flags = CardMechanicFlags.None;
        if (spec.Damage > 0)
            flags |= CardMechanicFlags.HasDamage;
        if (spec.Block > 0)
            flags |= CardMechanicFlags.HasBlock;
        if (spec.AppliedVulnerable > 0)
            flags |= CardMechanicFlags.AppliesVulnerable;

        profile = new CardMechanicProfile(
            cardId,
            flags,
            [],
            spec.Cost,
            spec.Damage > 0 ? spec.Damage : null,
            spec.Block > 0 ? spec.Block : null,
            spec.CardType,
            [],
            spec.AppliedVulnerable,
            spec.AppliedWeak);

        return true;
    }
}
