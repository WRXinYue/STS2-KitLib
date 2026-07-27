using System;
using System.Text;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

/// <summary>Verbose diagnostics for block-scaling attacks (Body Slam) defer / beam picks.</summary>
internal static class BlockScalingDecisionDiag {
    public static bool HandHasBlockScaling(CombatState state) {
        for (int i = 0; i < state.Hand.Count; i++) {
            if (BlockDefensePolicy.IsBlockScalingAttack(state.Hand[i]))
                return true;
        }

        return false;
    }

    public static string FormatContext(CombatState state, int pickedHandIndex = -1, int pickedEnemyIndex = -1) {
        if (!HandHasBlockScaling(state))
            return "";

        var sb = new StringBuilder(" BLK_ATK=");
        sb.Append($"blk={state.PlayerBlock} net={BlockDefensePolicy.NetDamage(state)}");
        sb.Append(BlockDefensePolicy.NeedsBlock(state) ? " needsBlk=1" : " needsBlk=0");
        sb.Append(BlockDefensePolicy.CanSkipBlockForKill(state) ? " skipBlkKill=1" : " skipBlkKill=0");
        sb.Append(BlockDefensePolicy.HasAffordablePureBlock(state) ? " pureBlk=1" : " pureBlk=0");
        sb.Append($" reserve={BlockDefensePolicy.BlockScalingAttackEnergyReserve(state)}");
        int peekBlk = KeyCardBurnRiskEvaluator.PeekTopPureBlockAtBurnRisk(state);
        if (peekBlk > 0)
            sb.Append($" peekBlk={peekBlk}");
        if (KeyCardBurnRiskEvaluator.HasAffordableDrawTopBurnEnabler(state))
            sb.Append(" drawBurn=1");

        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!BlockDefensePolicy.IsBlockScalingAttack(card))
                continue;
            if (!CombatCardCost.CanAfford(card, state))
                continue;

            int dmg = CombatDamageCalc.OutgoingDamage(card, state);
            bool defer = BlockDefensePolicy.ShouldDeferBlockScalingAttack(state, card, i, pickedEnemyIndex);
            sb.Append($" {card.Id}:dmg={dmg} scale={card.Profile.HitScaleMode} defer={(defer ? 1 : 0)}");
            sb.Append($"({ExplainDefer(state, card, i, pickedEnemyIndex)})");
        }

        if (pickedHandIndex >= 0
            && pickedHandIndex < state.Hand.Count
            && BlockDefensePolicy.IsBlockScalingAttack(state.Hand[pickedHandIndex])) {
            sb.Append(" PICK=slam-opener");
        }

        return sb.ToString();
    }

    public static string ExplainDeferPublic(
        CombatState state,
        CombatHandCard card,
        int handIndex,
        int enemyIndex) =>
        ExplainDefer(state, card, handIndex, enemyIndex);

    static string ExplainDefer(
        CombatState state,
        CombatHandCard card,
        int handIndex,
        int enemyIndex) {
        if (!BlockDefensePolicy.IsBlockScalingAttack(card))
            return "not-slam";
        if (handIndex >= 0
            && enemyIndex >= 0
            && SimLethalChecker.CanKillEnemyThisAction(state, handIndex, enemyIndex))
            return "lethal";

        int slamDamage = CombatDamageCalc.OutgoingDamage(card, state);
        if (slamDamage <= 0)
            return BlockDefensePolicy.HasAffordablePureBlock(state, handIndex) ? "zero-dmg" : "zero-no-block";

        if (!BlockDefensePolicy.NeedsBlock(state)) {
            if (state.PlayerBlock > 0)
                return "safe-has-blk";
            if (!BlockDefensePolicy.HasAffordablePureBlock(state, handIndex))
                return "safe-no-block-card";
            return ExplainBuildBeforeSlam(state, card, handIndex, slamDamage);
        }

        if (!BlockDefensePolicy.HasAffordablePureBlock(state, handIndex))
            return ExplainNoPureBlock(state, handIndex);

        int net = BlockDefensePolicy.NetDamage(state);
        if (slamDamage < net)
            return $"slam<{net}";

        return ExplainBuildBeforeSlam(state, card, handIndex, slamDamage);
    }

    static string ExplainBuildBeforeSlam(
        CombatState state,
        CombatHandCard card,
        int handIndex,
        int slamDamage) {
        int reserve = BlockDefensePolicy.BlockScalingAttackEnergyReserve(state);
        int extraBlock = BlockDefensePolicy.BestAffordablePureBlockAfterReserve(state, handIndex, reserve);
        if (extraBlock <= 0)
            return "no-extra-block";

        int slamAfter = CombatDamageCalc.OutgoingDamage(
            card,
            state with { PlayerBlock = state.PlayerBlock + extraBlock });
        if (slamAfter > slamDamage)
            return $"build+{extraBlock}->{slamAfter}";

        return "play-ok";
    }

    static string ExplainNoPureBlock(CombatState state, int handIndex) {
        int peekBlk = KeyCardBurnRiskEvaluator.PeekTopPureBlockAtBurnRisk(state, handIndex);
        if (peekBlk > 0)
            return $"peek-block+{peekBlk}-burn-risk";
        return "no-pure-block";
    }
}
