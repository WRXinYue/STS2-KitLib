namespace KitLib.AI.Knowledge;

/// <summary>How attack hit count is derived at play time (official CalculatedHits / X-cost).</summary>
public enum AttackHitScaleMode {
    None,
    Energy,
    AttacksPlayedThisTurn,
    SkillsInHand,
    OrbCount,
    StatusCardsOwned,
    UnblockedDamageTakenPlusOne,
}
