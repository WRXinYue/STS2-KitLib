using System;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

public static class PlayerCombatModifierRegistry {
    public static PlayerCombatModifier? FromSnapshot(JsonObject power) {
        var id = NormalizeId(power);
        if (string.IsNullOrEmpty(id))
            return null;

        int amount = power["amount"]?.GetValue<int>() ?? 1;
        return FromPowerId(id, amount);
    }

    public static PlayerCombatModifier FromMoveEffect(MonsterMoveEffect effect) {
        if (!string.IsNullOrWhiteSpace(effect.PowerId)) {
            var mapped = FromPowerId(effect.PowerId, Math.Max(1, effect.BoundCardsPerTurn > 0
                ? effect.BoundCardsPerTurn
                : effect.AttackCostPenalty > 0
                    ? effect.AttackCostPenalty
                    : 1));
            if (mapped != null)
                return mapped;
        }

        return new PlayerCombatModifier(
            effect.PowerId ?? "UNKNOWN",
            effect.AttackDamageMultiplier,
            BlockMultiplier: 1f,
            effect.SkillCostPenalty,
            effect.AttackCostPenalty,
            effect.BoundCardsPerTurn);
    }

    public static PlayerCombatModifier? FromPowerId(string id, int amount) {
        if (IsNonDeterministic(id))
            return null;

        if (id.Contains("SHRINK", StringComparison.Ordinal))
            return PlayerCombatModifier.Shrink();

        if (id.Contains("SMOG", StringComparison.Ordinal))
            return PlayerCombatModifier.Smoggy();

        if (id.Contains("TANGLE", StringComparison.Ordinal) || id.Contains("ENTANGLE", StringComparison.Ordinal))
            return PlayerCombatModifier.Tangled(Math.Max(1, amount));

        if (id.Contains("CHAIN", StringComparison.Ordinal) && id.Contains("BIND", StringComparison.Ordinal))
            return PlayerCombatModifier.ChainsOfBinding(Math.Max(1, amount));

        if (id.Contains("WEAK", StringComparison.Ordinal))
            return PlayerCombatModifier.Weak();

        if (id.Contains("FRAIL", StringComparison.Ordinal))
            return PlayerCombatModifier.Frail();

        if (id.Contains("STRENGTH", StringComparison.Ordinal))
            return PlayerCombatModifier.Strength(Math.Max(0, amount));

        if (id.Contains("DEXTERITY", StringComparison.Ordinal))
            return PlayerCombatModifier.Dexterity(Math.Max(0, amount));

        if (id.Contains("CONFUS", StringComparison.Ordinal))
            return PlayerCombatModifier.Confused();

        return null;
    }

    static bool IsNonDeterministic(string id) =>
        id.Contains("THIEV", StringComparison.Ordinal);

    static string NormalizeId(JsonObject power) =>
        (power["modelId"]?.GetValue<string>()
            ?? power["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
}
