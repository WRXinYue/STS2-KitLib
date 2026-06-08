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

    public static bool ShouldDeferSetup(CombatState state) =>
        ThreatModel.IncomingDamage(state) >= 8 && HasKillableAttacker(state);

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

        if (!SimLethalChecker.CanKillEnemyThisAction(state, action.HandIndex, action.EnemyIndex))
            return target.EffectiveIncoming * 4;

        return target.EffectiveIncoming * KillOpenerBonusPerIncoming;
    }

    public static int SetupOpenerPenalty(CombatState state, CombatHandCard card) {
        if (card.IsAttack && card.Damage > 0)
            return 0;
        if (CombatDamageCalc.OutgoingBlock(card, state) > 0)
            return 0;
        if (PlayerPowerSimulator.InstallsInferno(card.Profile))
            return 0;
        if (!ShouldDeferSetup(state))
            return 0;
        return SetupOpenerPenaltyAmount;
    }
}
