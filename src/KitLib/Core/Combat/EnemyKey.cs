using MegaCrit.Sts2.Core.Entities.Creatures;

namespace KitLib.Combat;

public static class EnemyKey {
    public static string Build(Creature enemy) {
        if (enemy.Monster is not { } monster)
            return enemy.GetHashCode().ToString();
        string slot = enemy.SlotName ?? enemy.GetHashCode().ToString();
        return $"{monster.Id.Entry}:{slot}";
    }
}
