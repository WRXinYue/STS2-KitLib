namespace KitLib.AI.Knowledge;

public sealed record PotionMechanicProfile(
    string Id,
    PotionCategory Category,
    string Usage,
    string TargetType,
    string Rarity,
    int EstimatedBlock,
    int EstimatedDamage);
