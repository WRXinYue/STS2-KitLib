using System;

namespace DevMode.AI.Knowledge;

[Flags]
public enum CardMechanicFlags : ulong {
    None = 0,
    TransformsCards = 1 << 0,
    TransformsHandAttacks = 1 << 1,
    HasDraw = 1 << 2,
    HasDiscard = 1 << 3,
    HasScry = 1 << 4,
    HasHeal = 1 << 5,
    HasSummon = 1 << 6,
    HasForge = 1 << 7,
    HasStarCost = 1 << 8,
    Exhaust = 1 << 9,
    Retain = 1 << 10,
    Ethereal = 1 << 11,
    Aoe = 1 << 12,
    HasDamage = 1 << 13,
    HasBlock = 1 << 14,
    AppliesVulnerable = 1 << 15,
    AppliesWeak = 1 << 16,
}
