using System;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat;

internal static class CombatCardStats {
    public static int ResolveDamage(JsonObject card) {
        var fromSnapshot = card["damage"]?.GetValue<int>();
        if (fromSnapshot is > 0)
            return fromSnapshot.Value;

        var id = card["id"]?.GetValue<string>();
        if (CardMechanicIndex.TryGet(id, out var profile) && profile.Damage is > 0)
            return profile.Damage.Value;

        return 0;
    }

    public static int ResolveBlock(JsonObject card) {
        var fromSnapshot = card["block"]?.GetValue<int>();
        if (fromSnapshot is > 0)
            return fromSnapshot.Value;

        var id = card["id"]?.GetValue<string>();
        if (CardMechanicIndex.TryGet(id, out var profile) && profile.Block is > 0)
            return profile.Block.Value;

        return 0;
    }

    public static CardMechanicProfile ResolveProfile(JsonObject card) =>
        CardMechanicIndex.TryGet(card["id"]?.GetValue<string>(), out var profile)
            ? profile
            : CardMechanicIndex.InferFromSnapshot(card);

    public static int CountHandAttacks(JsonArray? hand) {
        if (hand == null) return 0;
        var count = 0;
        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            if (IsAttackCard(card))
                count++;
        }
        return count;
    }

    public static int EstimateFollowupAttackDamage(JsonArray? hand, int energy) {
        if (hand == null || energy <= 0) return 0;

        var attacks = new System.Collections.Generic.List<(int Cost, int Damage)>();
        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            if (!IsAttackCard(card)) continue;
            var cost = card["cost"]?.GetValue<int>() ?? 99;
            if (cost > energy) continue;
            attacks.Add((cost, ResolveDamage(card)));
        }

        attacks.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        var remaining = energy;
        var total = 0;
        foreach (var (cost, damage) in attacks) {
            if (cost > remaining) continue;
            remaining -= cost;
            total += damage;
        }

        return total;
    }

    public static bool IsAttackCard(JsonObject card) {
        var cardType = card["cardType"]?.GetValue<string>() ?? "";
        return cardType.Contains("Attack", StringComparison.OrdinalIgnoreCase)
            || ResolveDamage(card) > 0;
    }

    public static bool IsSkillCard(JsonObject card) {
        var cardType = card["cardType"]?.GetValue<string>() ?? "";
        return cardType.Contains("Skill", StringComparison.OrdinalIgnoreCase);
    }
}
