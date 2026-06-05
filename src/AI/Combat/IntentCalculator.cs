using System;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat.Simulation;

namespace DevMode.AI.Combat;

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

        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        if (floor <= BlockThreatEvaluator.EarlyFloorMax
            && net >= BlockThreatEvaluator.EarlyBlockThreshold)
            return true;

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
        var state = CombatState.FromSnapshot(snapshot);
        var threats = state.Enemies
            .Where(e => e.IsAlive && e.IntentDamage > 0)
            .ToList();

        if (threats.Count == 0) return true;

        var net = ThreatModel.NetDamageAfterBlock(state);
        if (net <= 0) return true;

        int maxDamage = SimLethalChecker.EstimateMaxDamage(state);

        if (AoeDamageEstimator.CanAoeLethalAll(state)
            && threats.All(t => AoeDamageEstimator.EstimateAoeKills(state, AoeDamageEstimator.MaxAoeDamage(state)) > 0))
            return net <= BlockThreatEvaluator.SafeLethalNetMax;

        if (ThreatModel.CanEliminateAllThreats(state, maxDamage)
            && SimLethalChecker.CanLethal(state, out _))
            return net <= BlockThreatEvaluator.SafeLethalNetMax;

        foreach (var threat in threats.OrderByDescending(t => t.IntentDamage)) {
            var afterKill = CombatSimulator.Apply(
                state,
                new SimCombatAction(SimActionKind.PlayCard, FindKillCardIndex(state, threat.Index), threat.Index));
            if (ThreatModel.IncomingDamage(afterKill) == 0
                && ThreatModel.NetDamageAfterBlock(afterKill) <= BlockThreatEvaluator.SafeLethalNetMax)
                return true;
        }

        return false;
    }

    static int FindKillCardIndex(CombatState state, int targetIndex) {
        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!card.CanPlay || !card.IsAttack || card.Cost > state.Energy) continue;
            return i;
        }
        return 0;
    }

    public static float HpRatio(JsonObject snapshot) {
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        return maxHp > 0 ? (float)hp / maxHp : 1f;
    }
}
