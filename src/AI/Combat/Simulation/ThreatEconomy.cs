namespace KitLib.AI.Combat.Simulation;

public static class ThreatEconomy {
    public static int ScaledNonDamagePressure(CombatState state) {
        var modeled = DeckPollutionEvaluator.ProjectedPollutionCost(state) / 3;
        if (modeled > 0)
            return modeled;

        return ThreatModel.ScaledNonDamagePressure(state);
    }
}
