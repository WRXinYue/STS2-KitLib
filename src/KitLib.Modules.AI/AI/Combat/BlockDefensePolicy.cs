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

    public static int AffordableBlockTotal(CombatState state) =>
        AffordableBlockWithBudget(state, state.Energy);

    /// <summary>Greedy block total when at most <paramref name="energyBudget"/> may be spent on block.</summary>
    public static int AffordableBlockWithBudget(CombatState state, int energyBudget) {
        if (energyBudget <= 0)
            return 0;

        var options = new List<(int Cost, int Block)>();
        foreach (var card in state.Hand) {
            if (!CombatCardCost.CanAfford(card, state)) continue;
            if (card.Block <= 0 && !card.IsSkill) continue;

            int block = CombatDamageCalc.OutgoingBlock(card, state);
            if (block <= 0) continue;

            options.Add((CombatCardCost.EffectiveCost(card, state), block));
        }

        options.Sort((a, b) => b.Block.CompareTo(a.Block));
        int energy = Math.Min(state.Energy, energyBudget);
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

    public static bool IsBlockScalingAttack(CombatHandCard card) =>
        card.Profile.HitScaleMode == AttackHitScaleMode.PlayerBlock
        || IsBlockScalingCardId(card.Id);

    public static bool HasAffordablePureBlock(CombatState state, int excludeHandIndex = -1) {
        for (int i = 0; i < state.Hand.Count; i++) {
            if (i == excludeHandIndex)
                continue;
            var card = state.Hand[i];
            if (!CombatCardCost.CanAfford(card, state))
                continue;
            if (IsPureBlockCard(card, state))
                return true;
        }

        return KeyCardBurnRiskEvaluator.PeekTopPureBlockAtBurnRisk(state, excludeHandIndex) > 0;
    }

    public static int BestAffordablePureBlockAfterReserve(
        CombatState state,
        int excludeHandIndex,
        int energyReserve) {
        int best = 0;
        for (int i = 0; i < state.Hand.Count; i++) {
            if (i == excludeHandIndex)
                continue;
            var card = state.Hand[i];
            if (!IsPureBlockCard(card, state))
                continue;
            int cost = CombatCardCost.EffectiveCost(card, state);
            if (cost > state.Energy - energyReserve)
                continue;
            best = Math.Max(best, CombatDamageCalc.OutgoingBlock(card, state));
        }

        int peekBlock = KeyCardBurnRiskEvaluator.PeekTopPureBlockAtBurnRisk(state, excludeHandIndex);
        if (peekBlock > 0)
            best = Math.Max(best, peekBlock);

        return best;
    }

    /// <summary>Block-scaling attacks should not open while affordable block would add more slam damage or cover net incoming.</summary>
    public static bool ShouldDeferBlockScalingAttack(
        CombatState state,
        CombatHandCard card,
        int handIndex = -1,
        int enemyIndex = -1) {
        if (!IsBlockScalingAttack(card))
            return false;

        if (handIndex >= 0
            && enemyIndex >= 0
            && SimLethalChecker.CanKillEnemyThisAction(state, handIndex, enemyIndex))
            return false;

        int slamDamage = CombatDamageCalc.OutgoingDamage(card, state);
        if (slamDamage <= 0)
            return HasAffordablePureBlock(state, handIndex);

        if (!NeedsBlock(state)) {
            if (state.PlayerBlock > 0 || !HasAffordablePureBlock(state, handIndex))
                return false;

            int reserve = BlockScalingAttackEnergyReserve(state);
            int extraBlock = BestAffordablePureBlockAfterReserve(state, handIndex, reserve);
            if (extraBlock <= 0)
                return false;

            int slamAfterBlock = CombatDamageCalc.OutgoingDamage(
                card,
                state with { PlayerBlock = state.PlayerBlock + extraBlock });
            return slamAfterBlock > slamDamage;
        }

        if (!HasAffordablePureBlock(state, handIndex))
            return false;

        int net = NetDamage(state);
        if (slamDamage < net)
            return true;

        int energyReserve = BlockScalingAttackEnergyReserve(state);
        int blockGain = BestAffordablePureBlockAfterReserve(state, handIndex, energyReserve);
        if (blockGain <= 0)
            return false;

        int slamAfterGain = CombatDamageCalc.OutgoingDamage(
            card,
            state with { PlayerBlock = state.PlayerBlock + blockGain });
        return slamAfterGain > slamDamage;
    }

    /// <summary>Energy to keep for affordable block-scaling attacks (e.g. Body Slam).</summary>
    public static int BlockScalingAttackEnergyReserve(CombatState state) {
        int reserve = 0;
        foreach (var card in state.Hand) {
            if (!CombatCardCost.CanAfford(card, state))
                continue;
            if (!CombatDamageCalc.DealsAttackDamage(card)
                || card.Profile.HitScaleMode != AttackHitScaleMode.PlayerBlock)
                continue;

            reserve = Math.Max(reserve, CombatCardCost.EffectiveCost(card, state));
        }

        return reserve;
    }

    /// <summary>Skip greedy block when already safe and slam damage is worth spending current block.</summary>
    public static bool ShouldSkipGreedyBlockForBlockScalingAttack(CombatState state) {
        if (NetDamage(state) > 0)
            return false;

        foreach (var card in state.Hand) {
            if (!CombatCardCost.CanAfford(card, state))
                continue;
            if (!IsBlockScalingAttack(card))
                continue;
            if (ShouldDeferBlockScalingAttack(state, card))
                continue;
            if (CombatDamageCalc.OutgoingDamage(card, state) > 0)
                return true;
        }

        return false;
    }

    internal static bool IsBlockScalingCardId(string? id) =>
        string.Equals(id, "BODY_SLAM", StringComparison.OrdinalIgnoreCase);
}
