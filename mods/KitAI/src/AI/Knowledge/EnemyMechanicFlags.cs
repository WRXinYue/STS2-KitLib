using System;

namespace KitLib.AI.Knowledge;

[Flags]
public enum EnemyMechanicFlags : ulong {
    None = 0,
    /// <summary>Spawn-time MinionPower / IsSecondaryEnemy.</summary>
    IsSecondaryEnemy = 1 << 0,
    /// <summary>IllusionPower revive loop (Parafright, EyeWithTeeth).</summary>
    HasIllusionRevive = 1 << 1,
    /// <summary>Move graph includes Summon intent.</summary>
    CanSummonAllies = 1 << 2,
    /// <summary>Summoned allies are marked with MinionPower.</summary>
    SpawnsWithMinionPower = 1 << 3,
    /// <summary>Summon without minion marking (e.g. TwoTailedRat).</summary>
    PeerSummon = 1 << 4,
    /// <summary>Boss phase respawn (AdaptablePower / TestSubject).</summary>
    CanBossPhaseRespawn = 1 << 5,
    HasDebuffIntent = 1 << 6,
    HasBuffIntent = 1 << 7,
    HasHealIntent = 1 << 8,
    HasDeathBlow = 1 << 9,
    /// <summary>SummonIntent but in-place transform (ToughEgg hatch).</summary>
    HatchesInPlace = 1 << 10,
    /// <summary>StatusIntent — adds Dazed/Slimed etc. to combat piles.</summary>
    HasStatusCardIntent = 1 << 11,
    /// <summary>Primary dies and spawns encounter minions (e.g. Phrog → Wriggler).</summary>
    SpawnsOnDeath = 1 << 12,
}
