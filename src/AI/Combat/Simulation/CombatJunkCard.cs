using System;

namespace KitLib.AI.Combat.Simulation;

public static class CombatJunkCard {
    public const int DefaultJunkValue = 12;

    public static bool IsJunkId(string? cardId, string? rarity = null) {
        if (string.IsNullOrWhiteSpace(cardId))
            return false;

        if (!string.IsNullOrWhiteSpace(rarity)) {
            if (rarity.Contains("CURSE", StringComparison.OrdinalIgnoreCase)
                || rarity.Contains("STATUS", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var upper = cardId.ToUpperInvariant();
        return upper.Contains("BURN", StringComparison.Ordinal)
            || upper.Contains("SLIMED", StringComparison.Ordinal)
            || upper.Contains("SLIME", StringComparison.Ordinal)
            || upper.Contains("WOUND", StringComparison.Ordinal)
            || upper.Contains("DAZED", StringComparison.Ordinal)
            || upper.Contains("DORMANT", StringComparison.Ordinal)
            || upper.Contains("VOID", StringComparison.Ordinal)
            || upper.Contains("INFECTION", StringComparison.Ordinal)
            || upper.Contains("TOXIC", StringComparison.Ordinal)
            || upper.Contains("BECKON", StringComparison.Ordinal)
            || upper.Contains("FRANTIC_ESCAPE", StringComparison.Ordinal);
    }
}
