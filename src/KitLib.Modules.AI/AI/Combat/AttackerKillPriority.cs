using System;
using System.Linq;
using KitLib.AI.Combat.Simulation;

namespace KitLib.AI.Combat;

/// <summary>Prefer killing this-turn attackers over chipping high-HP primaries.</summary>
internal static class AttackerKillPriority {
    public const int KillOpenerBonusPerIncoming = 14;
    public const int SetupOpenerPenaltyAmount = 120;

    public static bool HasKillableAttacker(CombatState state) {
        if (ThreatModel.IncomingDamage(state) <= 0)
            return false;

        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive || enemy.EffectiveIncoming <= 0)
                continue;
            if (!ThreatModel.IsViableAttackTarget(state, enemy))
                continue;
            if (LethalExclusions.ShouldSkip(enemy))
                continue;
            if (!PrimaryWipeEngagementPolicy.PreferMinionAttackerFocus(state, enemy))
                continue;
            if (CombatSetupEvaluator.EstimateGreedyAttackDamageOn(state, enemy.Index) >= enemy.EffectiveHp)
                return true;
        }

        return false;
    }

    public static bool ShouldDeferSetup(CombatState state) {
        if (ThreatModel.IncomingDamage(state) < 8)
            return false;
        if (HasKillableAttacker(state))
            return true;
        return ThreatModel.HasIncomingAttackers(state)
            && !BlockDefensePolicy.CanFullyBlock(state);
    }

    public static int BlockOpenerPenalty(CombatState state, CombatHandCard card) {
        if (CombatDamageCalc.OutgoingBlock(card, state) <= 0)
            return 0;
        if (ThreatModel.IncomingDamage(state) < 8)
            return 0;
        if (BlockDefensePolicy.CanFullyBlock(state))
            return 0;
        if (HasKillableAttacker(state) || ThreatModel.IsFatalIfUnblocked(state))
            return SetupOpenerPenaltyAmount;
        if (ThreatModel.HasIncomingAttackers(state))
            return SetupOpenerPenaltyAmount / 2;
        return 0;
    }

    public static int WrongTargetPenalty(CombatState state, int enemyIndex) {
        if (enemyIndex < 0 || !ThreatModel.HasIncomingAttackers(state))
            return 0;

        var target = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == enemyIndex);
        if (target == null || target.EffectiveIncoming > 0)
            return 0;

        return SetupOpenerPenaltyAmount / 2;
    }

    public static int OpenerBonus(CombatState state, SimCombatAction action) {
        if (action.Kind != SimActionKind.PlayCard
            || action.HandIndex < 0
            || action.HandIndex >= state.Hand.Count
            || action.EnemyIndex < 0)
            return 0;

        var card = state.Hand[action.HandIndex];
        if (!card.IsAttack || card.Damage <= 0)
            return 0;

        var target = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == action.EnemyIndex);
        if (target == null || target.EffectiveIncoming <= 0)
            return 0;
        if (!PrimaryWipeEngagementPolicy.PreferMinionAttackerFocus(state, target))
            return 0;

        if (SimLethalChecker.CanKillEnemyThisAction(state, action.HandIndex, action.EnemyIndex))
            return target.EffectiveIncoming * KillOpenerBonusPerIncoming;

        int bonus = target.EffectiveIncoming * 4;
        if (target.Index == ThreatModel.HighestIncomingAttackerIndex(state))
            bonus = Math.Max(bonus, target.EffectiveIncoming * 6);
        if (ThreatModel.IsFatalIfUnblocked(state))
            bonus += target.EffectiveIncoming * 2;
        return bonus;
    }

    public static int SetupOpenerPenalty(CombatState state, CombatHandCard card) {
        if (card.IsAttack && card.Damage > 0)
            return 0;
        if (CombatDamageCalc.OutgoingBlock(card, state) > 0)
            return BlockOpenerPenalty(state, card);
        if (PlayerPowerSimulator.InstallsInferno(card.Profile))
            return 0;
        if (!ShouldDeferSetup(state))
            return 0;
        return SetupOpenerPenaltyAmount;
    }
}
