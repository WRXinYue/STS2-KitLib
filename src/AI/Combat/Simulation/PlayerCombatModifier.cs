namespace DevMode.AI.Combat.Simulation;

public sealed record PlayerCombatModifier(
    string PowerId,
    float AttackDamageMultiplier = 1f,
    int SkillCostPenalty = 0,
    int AttackCostPenalty = 0,
    int BoundCardsPerTurn = 0) {
    public static PlayerCombatModifier Shrink() =>
        new("SHRINK", AttackDamageMultiplier: 0.7f);

    public static PlayerCombatModifier Smoggy() =>
        new("SMOGGY", SkillCostPenalty: 1);

    public static PlayerCombatModifier Tangled() =>
        new("TANGLED", AttackCostPenalty: 1);

    public static PlayerCombatModifier ChainsOfBinding() =>
        new("CHAINS_OF_BINDING", BoundCardsPerTurn: 3);
}
