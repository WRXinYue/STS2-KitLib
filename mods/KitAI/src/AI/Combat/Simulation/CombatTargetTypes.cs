using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

internal static class CombatTargetTypes {
    public static bool IsAllEnemies(string? targetType) =>
        targetType is "AllEnemies" or "AllEnemy";

    public static bool IsAnyEnemy(string? targetType) =>
        targetType is "AnyEnemy" or "AllEnemy" or "AllEnemies";

    public static bool NeedsEnemyTarget(string? targetType, bool isAttack) =>
        IsAnyEnemy(targetType)
        || (isAttack && string.IsNullOrEmpty(targetType));

    public static bool NeedsEnemyTarget(CombatHandCard card) {
        if (IsAllEnemies(card.TargetType))
            return false;
        if (NeedsEnemyTarget(card.TargetType, card.IsAttack))
            return true;
        return AppliesDirectedDebuff(card.Profile);
    }

    public static bool AppliesDirectedDebuff(CardMechanicProfile profile) =>
        profile.AppliedVulnerable > 0
        || profile.AppliedWeak > 0
        || profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable)
        || profile.Flags.HasFlag(CardMechanicFlags.AppliesWeak);
}
