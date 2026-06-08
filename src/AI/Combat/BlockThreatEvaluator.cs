using System;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

/// <summary>Shared incoming-damage threat checks for block scoring and transform suppression.</summary>
public static class BlockThreatEvaluator {
    public const int EarlyFloorMax = 15;
    public const int EarlyBlockThreshold = 6;
    public const int LateBlockThreshold = 8;
    [Obsolete("Use BlockDefensePolicy.CanSkipBlockForKill instead.")]
    public const int SafeLethalNetMax = 0;
    public const float ThreatDiscountFloor = 0.4f;

    public static bool HasIncomingDamage(JsonObject snapshot) =>
        IntentCalculator.TotalIncomingDamage(snapshot) > 0;

    public static int EarlyBlockThresholdFor(JsonObject snapshot) {
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        return floor <= EarlyFloorMax ? EarlyBlockThreshold : LateBlockThreshold;
    }

    public static bool ShouldScoreBlock(JsonObject snapshot) =>
        BlockDefensePolicy.ShouldScoreBlock(snapshot);

    /// <summary>True only for fatal or real NeedsBlock threat (not mild score-block alone).</summary>
    public static bool ShouldSuppressTransform(JsonObject snapshot) {
        if (IntentCalculator.IsFatalIfUnblocked(snapshot))
            return true;

        return BlockDefensePolicy.NeedsBlock(snapshot);
    }

    /// <summary>Threat discount for transform follow-up (1.0 = safe, floor 0.4 under high urgency).</summary>
    public static float ThreatDiscountScale(JsonObject snapshot) {
        var urgency = IntentCalculator.BlockUrgency(snapshot);
        return Math.Max(ThreatDiscountFloor, 1f - urgency / 100f * 0.6f);
    }

    public static bool HasAffordableHandTransform(JsonArray? hand, int energy) =>
        FindAffordableHandTransform(hand, energy).transformCard != null;

    public static (JsonObject? transformCard, int index) FindAffordableHandTransform(JsonArray? hand, int energy) {
        if (hand == null) return (null, -1);

        var index = CombatTransformSimulator.FindHandAttackTransformIndex(hand);
        if (index < 0) return (null, -1);

        var card = hand[index]?.AsObject();
        if (card == null) return (null, -1);
        if (card["canPlay"]?.GetValue<bool>() == false) return (null, -1);

        var cost = card["cost"]?.GetValue<int>() ?? 99;
        if (cost > energy) return (null, -1);
        if (CombatTransformSimulator.CountTransformableAttacks(hand) == 0) return (null, -1);

        return (card, index);
    }

    public static int TransformDamageGain(JsonArray? hand, JsonObject transformCard) =>
        CombatTransformSimulator.EstimateDamageGain(hand, transformCard);

    public static bool IsStarterDefend(string? cardId, string? rarity = null) {
        if (string.IsNullOrWhiteSpace(cardId))
            return false;

        var idUpper = cardId.ToUpperInvariant();
        if (idUpper.Contains("DEFEND", StringComparison.Ordinal))
            return true;

        if (!string.IsNullOrWhiteSpace(rarity)
            && rarity.ToUpperInvariant().Contains("STARTER", StringComparison.Ordinal))
            return true;

        return false;
    }
}
