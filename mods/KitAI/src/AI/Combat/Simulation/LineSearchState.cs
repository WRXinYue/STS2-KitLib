using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

/// <summary>Whether a sim state has no further plays on the current beam line.</summary>
internal static class LineSearchState {
    public static bool IsExhausted(CombatState state) {
        if (state.AliveEnemyCount == 0)
            return true;
        if (CombatCardCost.HasAffordablePlay(state))
            return false;
        return !HasAffordablePotion(state);
    }

    static bool HasAffordablePotion(CombatState state) {
        if (state.PotionUsedThisTurn || state.Potions.Count == 0)
            return false;

        foreach (var potion in state.Potions) {
            if (PotionCombatEffectData.TryGetProfile(potion.Id, out var profile) && profile.Simulatable)
                return true;
        }

        return false;
    }
}
