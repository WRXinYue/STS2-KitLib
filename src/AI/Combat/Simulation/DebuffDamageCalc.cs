using System;

namespace KitLib.AI.Combat.Simulation;

/// <summary>Enemy weak on attackers — matches <see cref="PlayerCombatModifier.Weak"/> (25% less).</summary>
internal static class DebuffDamageCalc {
    public const float WeakIncomingMultiplier = 0.75f;

    public static int MitigateWeakIncoming(int damage, int weakStacks) {
        if (damage <= 0 || weakStacks <= 0)
            return damage;
        return Math.Max(0, (int)Math.Round(damage * WeakIncomingMultiplier));
    }

    public static int WeakIncomingSaved(int damage, int weakStacks) =>
        Math.Max(0, damage - MitigateWeakIncoming(damage, weakStacks));
}
