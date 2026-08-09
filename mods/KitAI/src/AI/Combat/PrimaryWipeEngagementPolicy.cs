using System.Linq;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

/// <summary>
/// Prefer focusing the primary when killing it wipes disposable minions (e.g. Kin priest).
/// </summary>
public static class PrimaryWipeEngagementPolicy {
    public static bool ShouldRushPrimary(CombatState state) {
        if (state.AliveEnemyCount < 2)
            return false;

        var primary = RushPrimaryTarget(state);
        if (primary == null)
            return false;

        int minionHp = state.Enemies
            .Where(e => e.IsAlive && e.IsMinion
                && !e.MechanicFlags.HasFlag(EnemyMechanicFlags.HasIllusionRevive))
            .Sum(e => e.CurrentHp);
        if (minionHp <= 0)
            return false;

        return primary.CurrentHp * 2 >= minionHp;
    }

    public static CombatEnemy? RushPrimaryTarget(CombatState state) {
        CombatEnemy? best = null;
        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive || enemy.IsMinion)
                continue;
            if (enemy.MechanicFlags.HasFlag(EnemyMechanicFlags.PeerSummon))
                continue;
            if (!ThreatModel.IsViableAttackTarget(state, enemy))
                continue;
            if (!MinionEngagementPolicy.ShouldSimulateMinionWipe(enemy, state.Enemies.ToArray()))
                continue;

            if (best == null
                || ThreatModel.FocusThreatScore(enemy, state) > ThreatModel.FocusThreatScore(best, state)
                || (ThreatModel.FocusThreatScore(enemy, state) == ThreatModel.FocusThreatScore(best, state)
                    && enemy.CurrentHp > best.CurrentHp)) {
                best = enemy;
            }
        }

        return best;
    }

    public static bool PreferMinionAttackerFocus(CombatState state, CombatEnemy enemy) {
        if (!ShouldRushPrimary(state))
            return true;
        if (!enemy.IsMinion || enemy.EffectiveIncoming <= 0)
            return true;

        if (!ThreatModel.IsFatalIfUnblocked(state))
            return false;

        var primary = RushPrimaryTarget(state);
        if (primary == null)
            return true;

        if (CombatSetupEvaluator.EstimateGreedyAttackDamageOn(state, primary.Index) >= primary.EffectiveHp)
            return false;

        return CombatSetupEvaluator.EstimateGreedyAttackDamageOn(state, enemy.Index) >= enemy.EffectiveHp;
    }
}
