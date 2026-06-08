using System;

namespace KitLib.AI.Knowledge;

[Flags]
public enum RelicMechanicFlags : ulong {
    None = 0,
    OffersRarePick = 1 << 0,
    OffersCardPick = 1 << 1,
    AddsCurseOrInjury = 1 << 2,
    AddsMaxHp = 1 << 3,
    GrantsGold = 1 << 4,
    GrantsPotion = 1 << 5,
    RemovesCard = 1 << 6,
    TransformsCards = 1 << 7,
    CombatScaling = 1 << 8,
}
