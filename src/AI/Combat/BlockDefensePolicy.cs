using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

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

            options.Add((CombatCardCost.EffectiveCost(card, state), block));
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

    public static bool NeedsBlock(JsonObject snapshot) =>
        NeedsBlock(CombatState.FromSnapshot(snapshot));

    public static bool NeedsBlock(CombatState state) {
        if (NetDamage(state) <= 0)
            return false;
        return !CanSkipBlockForKill(state);
    }

    public static bool ShouldScoreBlock(JsonObject snapshot) =>
        NeedsBlock(snapshot);

    public static bool CanSkipBlockForKill(JsonObject snapshot) =>
        CanSkipBlockForKill(CombatState.FromSnapshot(snapshot));

    public static bool CanSkipBlockForKill(CombatState state) {
        if (NetDamage(state) <= SafeChipNetMax)
            return true;

        return SimLethalChecker.CanSecureKillThisTurn(state);
    }

    public static bool IsPureBlockCard(CombatHandCard card, CombatState state) {
        if (card.Profile.AppliedVulnerable > 0 || card.Profile.AppliedWeak > 0)
            return false;

        return CombatDamageCalc.OutgoingBlock(card, state) > 0
            && card.Damage <= 0
            && !card.IsAttack;
    }

    public static bool IsPureBlockOpening(CombatState root, SimCombatAction action) {
        if (action.Kind != SimActionKind.PlayCard
            || action.HandIndex < 0
            || action.HandIndex >= root.Hand.Count)
            return false;

        return IsPureBlockCard(root.Hand[action.HandIndex], root);
    }
}
