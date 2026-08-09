namespace KitLib.AI.Combat.Simulation;

public sealed record CombatIntentStep(
    string MoveId,
    int IntentDamage,
    bool IsUncertain,
    string[] IntentTypes,
    int NonDamageThreat);
