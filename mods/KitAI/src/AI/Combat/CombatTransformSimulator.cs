using System;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

/// <summary>Simulates hand-attack transforms (Primal Force → Giant Rock per official card logic).</summary>
internal static class CombatTransformSimulator {
    const string GiantRockId = "GIANT_ROCK";
    const int GiantRockUpgradeBonus = 4;

    public static bool IsHandAttackTransform(CardMechanicProfile profile) =>
        profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks);

    public static bool IsTransformableAttack(JsonObject card) {
        if (!CombatCardStats.IsAttackCard(card))
            return false;

        var id = card["id"]?.GetValue<string>() ?? "";
        if (id.Equals(GiantRockId, StringComparison.OrdinalIgnoreCase))
            return false;

        var keywords = card["keywords"]?.AsArray();
        if (keywords != null) {
            foreach (var kw in keywords) {
                var name = kw?.GetValue<string>() ?? "";
                if (name.Contains("Eternal", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }

    public static int CountTransformableAttacks(JsonArray? hand) {
        if (hand == null) return 0;
        var count = 0;
        foreach (var node in hand) {
            if (node is JsonObject card && IsTransformableAttack(card))
                count++;
        }
        return count;
    }

    public static int GiantRockDamage(bool upgraded) {
        var baseDamage = 16;
        if (CardMechanicIndex.TryGet(GiantRockId, out var profile) && profile.Damage is > 0)
            baseDamage = profile.Damage.Value;
        return upgraded ? baseDamage + GiantRockUpgradeBonus : baseDamage;
    }

    public static JsonObject MakeGiantRockCard(bool upgraded) {
        var damage = GiantRockDamage(upgraded);
        return new JsonObject {
            ["id"] = GiantRockId,
            ["name"] = "Giant Rock",
            ["cost"] = 1,
            ["cardType"] = "Attack",
            ["targetType"] = "AnyEnemy",
            ["damage"] = damage,
            ["canPlay"] = true,
        };
    }

    /// <summary>Greedy max attack damage this turn after transform minus before (accounts for energy).</summary>
    public static int EstimateTurnDamageDelta(JsonArray? hand, JsonObject transformSkill, int energy) {
        if (hand == null) return int.MinValue;

        var skillCost = transformSkill["cost"]?.GetValue<int>() ?? 0;
        if (skillCost > energy) return int.MinValue;

        var before = CombatCardStats.EstimateFollowupAttackDamage(hand, energy);
        var projected = ProjectHandAfterTransform(hand, transformSkill);
        var after = CombatCardStats.EstimateFollowupAttackDamage(projected, energy - skillCost);
        return after - before;
    }

    public static int EstimateDamageGain(JsonArray? hand, JsonObject transformSkill) {
        if (hand == null) return 0;
        var upgraded = IsUpgraded(transformSkill);
        var rockDamage = GiantRockDamage(upgraded);
        var skillId = transformSkill["id"]?.GetValue<string>() ?? "";
        var gain = 0;

        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            if (string.Equals(card["id"]?.GetValue<string>(), skillId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!IsTransformableAttack(card)) continue;
            gain += Math.Max(0, rockDamage - CombatCardStats.ResolveDamage(card));
        }

        return gain;
    }

    public static JsonArray ProjectHandAfterTransform(JsonArray? hand, JsonObject transformSkill) {
        if (hand == null) return new JsonArray();
        var clone = hand.DeepClone()?.AsArray() ?? new JsonArray();
        var skillIndex = FindSkillIndex(clone, transformSkill);
        if (skillIndex >= 0)
            ApplyHandAttackTransform(clone, skillIndex);
        return clone;
    }

    public static void ApplyHandAttackTransform(JsonArray hand, int skillIndex) {
        if (skillIndex < 0 || skillIndex >= hand.Count) return;
        var skill = hand[skillIndex]?.AsObject();
        if (skill == null) return;

        var upgraded = IsUpgraded(skill);
        hand.RemoveAt(skillIndex);

        var rock = MakeGiantRockCard(upgraded);
        for (var i = 0; i < hand.Count; i++) {
            if (hand[i] is JsonObject card && IsTransformableAttack(card))
                hand[i] = rock.DeepClone();
        }
    }

    public static int FindHandAttackTransformIndex(JsonArray? hand) {
        if (hand == null) return -1;
        for (var i = 0; i < hand.Count; i++) {
            if (hand[i] is not JsonObject card) continue;
            var profile = CombatCardStats.ResolveProfile(card);
            if (IsHandAttackTransform(profile))
                return i;
        }
        return -1;
    }

    static int FindSkillIndex(JsonArray hand, JsonObject transformSkill) {
        var skillId = transformSkill["id"]?.GetValue<string>() ?? "";
        for (var i = 0; i < hand.Count; i++) {
            if (hand[i] is not JsonObject card) continue;
            if (string.Equals(card["id"]?.GetValue<string>(), skillId, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    static bool IsUpgraded(JsonObject card) =>
        (card["upgradeLevel"]?.GetValue<int>() ?? 0) > 0;
}
