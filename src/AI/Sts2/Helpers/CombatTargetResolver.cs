using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Sts2.Helpers;

/// <summary>
/// Resolves live combat creatures by stable combat index — same numbering as
/// <see cref="Snapshots.GameSnapshot"/> <c>enemy["index"]</c> (0-based slot in <c>CombatState.Enemies</c>).
/// </summary>
internal static class CombatTargetResolver {
    public static Creature? ResolveEnemy(CombatState combatState, CardModel card, int combatIndex) {
        var preferred = FindEnemyByCombatIndex(combatState, combatIndex);
        if (preferred != null && card.IsValidTarget(preferred))
            return preferred;

        return combatState.HittableEnemies.FirstOrDefault(card.IsValidTarget);
    }

    public static Creature? ResolveHittableEnemy(CombatState combatState, int combatIndex) {
        var preferred = FindEnemyByCombatIndex(combatState, combatIndex);
        if (preferred != null && combatState.HittableEnemies.Contains(preferred))
            return preferred;

        return combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive);
    }

    public static Creature? FindEnemyByCombatIndex(CombatState combatState, int combatIndex) {
        if (combatIndex < 0)
            return null;

        int slot = 0;
        foreach (var enemy in combatState.Enemies) {
            if (slot == combatIndex)
                return enemy.IsAlive ? enemy : null;
            slot++;
        }

        return null;
    }
}
