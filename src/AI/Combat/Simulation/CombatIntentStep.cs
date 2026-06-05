namespace DevMode.AI.Combat.Simulation;

public sealed record CombatIntentStep(
    string MoveId,
    int IntentDamage,
    bool IsUncertain);
