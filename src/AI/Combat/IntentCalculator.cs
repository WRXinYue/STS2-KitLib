using System;
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

    public static bool NeedsBlock(JsonObject snapshot) {
        var net = NetDamageAfterBlock(snapshot);
        if (net <= 0) return false;

        if (LethalChecker.CanLethal(snapshot, out _))
            return false;

        if (CanRaceKill(snapshot))
            return false;

        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var statusThreat = EstimateStatusDamage(snapshot);
        var effectiveHp = Math.Max(1, hp - statusThreat);

        var thresholdRatio = Math.Max(12, (int)(effectiveHp * 0.25f));
        return net >= thresholdRatio || net >= effectiveHp - 10;
    }

    static bool CanRaceKill(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        var hand = combat?["hand"]?.AsArray();
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var enemies = combat?["enemies"]?.AsArray();
        if (hand == null || enemies == null) return false;

        var maxDamage = LethalChecker.EstimateMaxDamage(hand, energy, 0);
        if (maxDamage <= 0) return false;

        foreach (var node in enemies) {
            if (node is not JsonObject enemy) continue;
            if (enemy["isAlive"]?.GetValue<bool>() == false) continue;

            var intent = enemy["intentDamage"]?.GetValue<int>() ?? 0;
            if (intent <= 0) continue;

            var hp = enemy["currentHp"]?.GetValue<int>() ?? 0;
            var block = enemy["block"]?.GetValue<int>() ?? 0;
            if (maxDamage >= hp + block)
                return true;
        }

        return false;
    }

    public static float HpRatio(JsonObject snapshot) {
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        return maxHp > 0 ? (float)hp / maxHp : 1f;
    }
}
