namespace KitLib.AI.Knowledge;

public sealed record MonsterMoveEffect(
    MonsterMoveEffectKind Kind,
    string? CardId = null,
    int Count = 0,
    string Pile = "Discard",
    string? SpawnMonsterId = null,
    string? PowerId = null,
    float AttackDamageMultiplier = 1f,
    int SkillCostPenalty = 0,
    int AttackCostPenalty = 0,
    int BoundCardsPerTurn = 0,
    int Damage = 0,
    int StrengthDelta = 0,
    bool IsNonDeterministic = false);
