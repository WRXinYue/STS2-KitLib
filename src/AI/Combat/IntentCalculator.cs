using System;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;

namespace KitLib.AI.Combat;

public static class IntentCalculator {
    public static int TotalIncomingDamage(JsonObject snapshot) =>
        ThreatModel.IncomingDamage(snapshot);

    public static int PlayerBlock(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        return combat?["playerBlock"]?.GetValue<int>() ?? 0;
    }

    public static int NetDamageAfterBlock(JsonObject snapshot) =>
        ThreatModel.NetDamageAfterBlock(snapshot);

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
    public static int BlockUrgency(JsonObject snapshot) =>
        BlockUrgencyFromState(CombatState.FromSnapshot(snapshot));

    public static int BlockUrgencyFromState(CombatState state) {
        var net = ThreatModel.NetDamageAfterBlock(state);
        if (net <= 0) return 0;

        var effectiveHp = ThreatModel.EffectiveHp(state);
        var ratio = (float)net / effectiveHp;
        var urgency = (int)(ratio * 60f);
        if (net >= effectiveHp) urgency += 40;
        if (state.PlayerMaxHp > 0 && (float)state.PlayerHp / state.PlayerMaxHp < 0.45f) urgency += 15;
        if (state.AliveEnemyCount >= 2) urgency += 10;
        return Math.Clamp(urgency, 0, 100);
    }

    public static bool NeedsBlock(JsonObject snapshot) =>
        BlockDefensePolicy.NeedsBlock(snapshot);

    public static float HpRatio(JsonObject snapshot) {
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        return maxHp > 0 ? (float)hp / maxHp : 1f;
    }
}
