using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat.Simulation;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat;

/// <summary>Shared block-vs-kill policy for snapshot scoring and beam simulation.</summary>
public static class BlockDefensePolicy {
    public const int SafeChipNetMax = 0;

    public static int NetDamage(CombatState state) =>
        ThreatModel.NetDamageAfterBlock(state);

    public static int NetDamage(JsonObject snapshot) =>
        ThreatModel.NetDamageAfterBlock(snapshot);

    public static int IncomingDamage(CombatState state) =>
        ThreatModel.IncomingDamage(state);

    public static int BlockGap(CombatState state) =>
        Math.Max(0, NetDamage(state));

    public static bool IsFullyBlocked(CombatState state) =>
        NetDamage(state) <= 0;

    public static int AffordableBlockTotal(CombatState state) {
        var options = new List<(int Cost, int Block)>();
        foreach (var card in state.Hand) {
            if (!CombatCardCost.CanAfford(card, state)) continue;
            if (card.Block <= 0 && !card.IsSkill) continue;

            int block = CombatDamageCalc.OutgoingBlock(card, state);
            if (block <= 0) continue;

            options.Add((CombatCardCost.EffectiveCost(card, state.Modifiers), block));
        }

        options.Sort((a, b) => b.Block.CompareTo(a.Block));
        int energy = state.Energy;
        int total = 0;
        foreach (var (cost, block) in options) {
            if (cost > energy) continue;
            energy -= cost;
            total += block;
        }

        return total;
    }

    public static int RemainingBlockGap(CombatState state) {
        var net = NetDamage(state);
        if (net <= 0) return 0;
        return Math.Max(0, net - AffordableBlockTotal(state));
    }

    public static bool CanFullyBlock(CombatState state) =>
        NetDamage(state) <= AffordableBlockTotal(state);

    public static bool ShouldPrioritizeBlock(CombatState state) =>
        NetDamage(state) > 0 && RemainingBlockGap(state) > 0;

    public static int FullBlockValue(CombatState state) {
        var net = NetDamage(state);
        if (net <= 0) return 0;

        var coverable = Math.Min(net, AffordableBlockTotal(state));
        return coverable * CombatEvalWeights.BlockCoverPerPoint;
    }

    public static bool NeedsBlock(JsonObject snapshot) {
        var net = NetDamage(snapshot);
        if (net <= 0) return false;

        var effectiveHp = IntentCalculator.EffectiveHp(snapshot);
        if (net >= effectiveHp) return true;

        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        if (floor <= BlockThreatEvaluator.EarlyFloorMax
            && net >= BlockThreatEvaluator.EarlyBlockThreshold)
            return true;

        return NeedsBlock(CombatState.FromSnapshot(snapshot));
    }

    public static bool NeedsBlock(CombatState state) {
        var net = NetDamage(state);
        if (net <= 0) return false;

        var effectiveHp = ThreatModel.EffectiveHp(state);
        if (net >= effectiveHp) return true;

        if (net >= BlockThreatEvaluator.LateBlockThreshold)
            return true;

        if (CanSkipBlockForKill(state))
            return false;

        var hpRatio = state.PlayerMaxHp > 0
            ? (float)state.PlayerHp / state.PlayerMaxHp
            : 1f;

        if (hpRatio < 0.55f)
            return true;

        var thresholdRatio = Math.Max(8, (int)(effectiveHp * 0.15f));
        if (net >= thresholdRatio || net >= effectiveHp - 20)
            return true;

        return IntentCalculator.BlockUrgencyFromState(state) >= 35;
    }

    public static bool ShouldScoreBlock(JsonObject snapshot) {
        if (NeedsBlock(snapshot))
            return true;

        return NetDamage(snapshot) >= BlockThreatEvaluator.EarlyBlockThresholdFor(snapshot);
    }

    public static bool CanSkipBlockForKill(JsonObject snapshot) =>
        CanSkipBlockForKill(CombatState.FromSnapshot(snapshot));

    public static bool CanSkipBlockForKill(CombatState state) {
        if (NetDamage(state) <= SafeChipNetMax)
            return true;

        return SimLethalChecker.CanSecureKillThisTurn(state);
    }
}
