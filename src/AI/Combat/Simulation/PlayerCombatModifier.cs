namespace DevMode.AI.Combat.Simulation;

public sealed record PlayerCombatModifier(
    string PowerId,
    float AttackDamageMultiplier = 1f,
    float BlockMultiplier = 1f,
    int SkillCostPenalty = 0,
    int AttackCostPenalty = 0,
    int BoundCardsPerTurn = 0) {
    public static PlayerCombatModifier Shrink() =>
        new("SHRINK", AttackDamageMultiplier: 0.7f);

    public static PlayerCombatModifier Smoggy() =>
        new("SMOGGY", SkillCostPenalty: 1);

    public static PlayerCombatModifier Tangled(int amount = 1) =>
        new("TANGLED", AttackCostPenalty: amount);

    public static PlayerCombatModifier ChainsOfBinding(int amount = 3) =>
        new("CHAINS_OF_BINDING", BoundCardsPerTurn: amount);

    public static PlayerCombatModifier Weak() =>
        new("WEAK", AttackDamageMultiplier: 0.75f);

    public static PlayerCombatModifier Frail() =>
        new("FRAIL", BlockMultiplier: 0.75f);
}
