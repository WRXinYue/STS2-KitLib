using System;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;

namespace KitLib.AI.AutoPlay.Scoring;

/// <summary>Macro potion belt value: retain scores, discard-to-make-room, shop offers.</summary>
public static class PotionInventoryScorer {
    public const int MakeRoomMargin = 8;

    public static int RetainedValue(JsonObject potion, JsonObject snapshot, bool inCombat = false) {
        var id = potion["id"]?.GetValue<string>() ?? "";
        var profile = PotionMechanicIndex.GetOrDefault(id);
        var category = ParseCategory(potion, profile);
        var score = potion["retainScore"]?.GetValue<int>() ?? PotionTierCatalog.GetRetainScore(id);

        if (!inCombat) {
            var hpRatio = HpRatio(snapshot);
            if (category == PotionCategory.Heal && hpRatio < 0.55f) score += 6;
            if (category == PotionCategory.Block && hpRatio < 0.7f) score += 4;
        }

        if (snapshot["hasOpenPotionSlots"]?.GetValue<bool>() == false)
            score += 2;

        score += DeckSynergyBonus(category, id, DeckPlanInferer.Infer(snapshot));
        return score;
    }

    public static int ValueIncoming(string? potionId, JsonObject snapshot) {
        if (string.IsNullOrWhiteSpace(potionId)) return 0;
        var profile = PotionMechanicIndex.GetOrDefault(potionId);
        var obj = new JsonObject {
            ["id"] = potionId,
            ["category"] = profile.Category.ToString(),
            ["retainScore"] = PotionTierCatalog.GetRetainScore(potionId),
        };
        return RetainedValue(obj, snapshot);
    }

    public static int ValueOffer(JsonObject offer, JsonObject snapshot) {
        var id = offer["id"]?.GetValue<string>() ?? "";
        return ValueIncoming(id, snapshot);
    }

    public static (int Slot, int Value)? FindLowestHeld(JsonObject snapshot) {
        var potions = snapshot["potions"]?.AsArray();
        if (potions == null || potions.Count == 0) return null;

        int? slot = null;
        int lowest = int.MaxValue;
        foreach (var node in potions) {
            if (node is not JsonObject potion) continue;
            var value = RetainedValue(potion, snapshot);
            var s = potion["slot"]?.GetValue<int>() ?? -1;
            if (s < 0 || value >= lowest) continue;
            lowest = value;
            slot = s;
        }

        return slot is int found ? (found, lowest) : null;
    }

    public static bool ShouldMakeRoom(string? incomingId, JsonObject snapshot, out int discardSlot) {
        discardSlot = -1;
        if (snapshot["hasOpenPotionSlots"]?.GetValue<bool>() != false) return false;

        var incoming = ValueIncoming(incomingId, snapshot);
        var lowest = FindLowestHeld(snapshot);
        if (lowest == null) return false;

        if (incoming <= lowest.Value.Value + MakeRoomMargin) return false;

        discardSlot = lowest.Value.Slot;
        return true;
    }

    static PotionCategory ParseCategory(JsonObject potion, PotionMechanicProfile profile) {
        var raw = potion["category"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(raw) && Enum.TryParse<PotionCategory>(raw, out var parsed))
            return parsed;
        return profile.Category;
    }

    static float HpRatio(JsonObject snapshot) {
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        return maxHp > 0 ? (float)hp / maxHp : 1f;
    }

    static int DeckSynergyBonus(PotionCategory category, string potionId, DeckPlan plan) =>
        category switch {
            PotionCategory.Buff => (int)Math.Round(plan.GetWeight(AiTag.Scaling) * 10f),
            PotionCategory.Debuff => (int)Math.Round(plan.GetWeight(AiTag.Attack) * 8f),
            PotionCategory.Block => (int)Math.Round(plan.GetWeight(AiTag.Block) * 8f),
            PotionCategory.Draw => (int)Math.Round(plan.GetWeight(AiTag.Draw) * 8f),
            PotionCategory.Energy => (int)Math.Round(
                Math.Max(plan.GetWeight(AiTag.Energy), plan.GetWeight(AiTag.Draw) * 0.6f) * 10f),
            PotionCategory.DamageSingle => (int)Math.Round(plan.GetWeight(AiTag.Attack) * 8f),
            PotionCategory.DamageAoE => (int)Math.Round(
                plan.GetWeight(AiTag.Aoe) * 8f + plan.GetWeight(AiTag.Attack) * 4f),
            PotionCategory.Random => RandomPoolSynergy(potionId, plan),
            PotionCategory.Heal => (int)Math.Round(plan.GetWeight(AiTag.Block) * 3f),
            _ => 0,
        };

    static int RandomPoolSynergy(string potionId, DeckPlan plan) {
        var upper = potionId.ToUpperInvariant();
        if (upper.Contains("ATTACK"))
            return (int)Math.Round(plan.GetWeight(AiTag.Attack) * 8f);
        if (upper.Contains("POWER"))
            return (int)Math.Round(plan.GetWeight(AiTag.Scaling) * 8f);
        if (upper.Contains("SKILL"))
            return (int)Math.Round(plan.GetWeight(AiTag.Block) * 4f + plan.GetWeight(AiTag.Draw) * 4f);
        return (int)Math.Round(plan.GetWeight(AiTag.Draw) * 6f);
    }
}
