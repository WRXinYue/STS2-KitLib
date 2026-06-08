namespace KitLib.AI.Combat.Simulation;

public sealed record PlayerCombatModifier(
    string PowerId,
    float AttackDamageMultiplier = 1f,
    float BlockMultiplier = 1f,
    int AttackDamageFlat = 0,
    int BlockFlat = 0,
    int SkillCostPenalty = 0,
    int AttackCostPenalty = 0,
    int BoundCardsPerTurn = 0,
    bool ConfusedDrawCostEv = false) {
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

    public static PlayerCombatModifier Strength(int amount) =>
        new("STRENGTH", AttackDamageFlat: amount);

    public static PlayerCombatModifier Dexterity(int amount) =>
        new("DEXTERITY", BlockFlat: amount);

    public static PlayerCombatModifier Confused() =>
        new("CONFUSED", ConfusedDrawCostEv: true);

    public static PlayerCombatModifier Focus(int amount) =>
        new("FOCUS", AttackDamageFlat: amount);

    public static PlayerCombatModifier Gigantification() =>
        new("GIGANTIFICATION", AttackDamageMultiplier: 2f);
}
