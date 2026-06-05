using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace DevMode.AI.Combat;

public static class IntentCalculator {
    public static int TotalIncomingDamage(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        var enemies = combat?["enemies"]?.AsArray();
        if (enemies == null) return 0;

        var total = 0;
        foreach (var node in enemies) {
            if (node is not JsonObject enemy) continue;
            if (enemy["isAlive"]?.GetValue<bool>() == false) continue;
            total += enemy["intentDamage"]?.GetValue<int>() ?? 0;
        }
        return total;
    }

    public static int PlayerBlock(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        return combat?["playerBlock"]?.GetValue<int>() ?? 0;
    }

    public static int NetDamageAfterBlock(JsonObject snapshot) {
        var incoming = TotalIncomingDamage(snapshot);
        var block = PlayerBlock(snapshot);
        return Math.Max(0, incoming - block);
    }

    public static int EstimateStatusDamage(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        var powers = combat?["playerPowers"]?.AsArray();
        if (powers == null) return 0;

        var total = 0;
        foreach (var node in powers) {
            if (node is not JsonObject power) continue;
            var id = (power["modelId"]?.GetValue<string>()
                ?? power["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
            var amount = power["amount"]?.GetValue<int>() ?? 0;
            if (amount <= 0) continue;

            if (id.Contains("BURN") || id.Contains("POISON") || id.Contains("INFEST")
                || id.Contains("DOOM"))
                total += amount;
        }
        return total;
    }

    public static int AliveEnemyCount(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        var enemies = combat?["enemies"]?.AsArray();
        if (enemies == null) return 0;

        return enemies.Count(e => e?["isAlive"]?.GetValue<bool>() != false);
    }

    public static int EffectiveHp(JsonObject snapshot) {
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var statusThreat = EstimateStatusDamage(snapshot);
        return Math.Max(1, hp - statusThreat);
    }

    public static bool IsFatalIfUnblocked(JsonObject snapshot) {
        var net = NetDamageAfterBlock(snapshot);
        return net >= EffectiveHp(snapshot);
    }

    /// <summary>0–100 scale for how urgently block is needed (scoring).</summary>
    public static int BlockUrgency(JsonObject snapshot) {
        var net = NetDamageAfterBlock(snapshot);
        if (net <= 0) return 0;

        var effectiveHp = EffectiveHp(snapshot);
        var ratio = (float)net / effectiveHp;
        var urgency = (int)(ratio * 60f);
        if (net >= effectiveHp) urgency += 40;
        if (HpRatio(snapshot) < 0.45f) urgency += 15;
        if (AliveEnemyCount(snapshot) >= 2) urgency += 10;
        return Math.Clamp(urgency, 0, 100);
    }

    public static bool NeedsBlock(JsonObject snapshot) {
        var net = NetDamageAfterBlock(snapshot);
        if (net <= 0) return false;

        var effectiveHp = EffectiveHp(snapshot);
        if (net >= effectiveHp) return true;

        if (CanEliminateIncomingThreats(snapshot))
            return false;

        if (LethalChecker.CanLethal(snapshot, out _)) {
            if (net <= Math.Max(6, effectiveHp / 5)) return false;
            if (HpRatio(snapshot) > 0.65f && net < effectiveHp / 3) return false;
        }

        var thresholdRatio = Math.Max(8, (int)(effectiveHp * 0.2f));
        return net >= thresholdRatio || net >= effectiveHp - 15;
    }

    /// <summary>
    /// True when this turn's max single-target damage can kill every enemy that would hit us.
    /// Multi-attacker fights still require block unless all threats are lethal this turn.
    /// </summary>
    static bool CanEliminateIncomingThreats(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        var hand = combat?["hand"]?.AsArray();
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var enemies = combat?["enemies"]?.AsArray();
        if (hand == null || enemies == null) return false;

        var threats = new List<int>();
        foreach (var node in enemies) {
            if (node is not JsonObject enemy) continue;
            if (enemy["isAlive"]?.GetValue<bool>() == false) continue;
            if ((enemy["intentDamage"]?.GetValue<int>() ?? 0) <= 0) continue;
            threats.Add((enemy["currentHp"]?.GetValue<int>() ?? 0)
                + (enemy["block"]?.GetValue<int>() ?? 0));
        }

        if (threats.Count == 0) return true;
        if (threats.Count > 1) return false;

        return EstimateMaxOffense(hand, energy) >= threats[0];
    }

    static int EstimateMaxOffense(JsonArray hand, int energy) {
        var max = LethalChecker.EstimateMaxDamage(hand, energy, 0);
        var transformIndex = CombatTransformSimulator.FindHandAttackTransformIndex(hand);
        if (transformIndex < 0) return max;

        var skill = hand[transformIndex]?.AsObject();
        if (skill == null) return max;

        var projected = CombatTransformSimulator.ProjectHandAfterTransform(hand, skill);
        var skillCost = skill["cost"]?.GetValue<int>() ?? 0;
        var afterTransform = LethalChecker.EstimateMaxDamage(
            projected, Math.Max(0, energy - skillCost), 0);
        return Math.Max(max, afterTransform);
    }

    public static float HpRatio(JsonObject snapshot) {
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        return maxHp > 0 ? (float)hp / maxHp : 1f;
    }
}
