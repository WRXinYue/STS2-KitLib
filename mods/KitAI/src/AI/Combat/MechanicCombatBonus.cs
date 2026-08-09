using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

/// <summary>Mechanic-index flags for setup skills (transform/debuff), not per-card scoring.</summary>
internal static class MechanicCombatBonus {
    public static bool IsSetupSkill(CardMechanicProfile profile) =>
        profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)
        || profile.Flags.HasFlag(CardMechanicFlags.TransformsCards)
        || profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable)
        || profile.Flags.HasFlag(CardMechanicFlags.AppliesWeak)
        || profile.Flags.HasFlag(CardMechanicFlags.PlaysTopOfDrawExhaust);
}
