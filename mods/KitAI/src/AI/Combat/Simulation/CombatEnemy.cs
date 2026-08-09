using System;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

public sealed record CombatEnemy(
    int Index,
    int CurrentHp,
    int MaxHp,
    int Block,
    bool IsAlive,
    bool IsMinion,
    int IntentDamage,
    int Vulnerable,
    int Weak,
    CombatIntentStep[] IntentSteps,
    EnemyMechanicFlags MechanicFlags = EnemyMechanicFlags.None,
    int NonDamageThreat = 0,
    int SummonerIndex = -1,
    string MonsterId = "",
    string NextMoveId = "",
    int ActOrder = 0,
    int Strength = 0) {
    public int EffectiveHp => CurrentHp + Block;

    /// <summary>HP damage this turn (snapshot intent already includes strength). <see cref="Strength"/> is for resolver mid-turn buffs.</summary>
    public int EffectiveIncoming =>
        IsAlive ? DebuffDamageCalc.MitigateWeakIncoming(IntentDamage, Weak) : 0;

    public CombatEnemy WithHp(int hp, int block, bool alive) =>
        this with { CurrentHp = hp, Block = block, IsAlive = alive };

    public CombatEnemy WithIntent(int intentDamage) =>
        this with { IntentDamage = intentDamage };

    public CombatEnemy WithMove(string nextMoveId, int intentDamage, int nonDamageThreat, CombatIntentStep[] steps) =>
        this with {
            NextMoveId = nextMoveId,
            IntentDamage = intentDamage,
            NonDamageThreat = nonDamageThreat,
            IntentSteps = steps,
        };

    public CombatEnemy WithPowers(int vulnerable, int weak) =>
        this with { Vulnerable = vulnerable, Weak = weak };

    public CombatEnemy WithStrength(int strength) =>
        this with { Strength = strength };

    public CombatEnemy AddStrength(int delta) =>
        this with { Strength = Math.Max(0, Strength + delta) };

    public CombatEnemy MarkDead() =>
        this with {
            CurrentHp = 0,
            Block = 0,
            IsAlive = false,
            IntentDamage = 0,
            NonDamageThreat = 0,
        };

    /// <summary>End-of-round debuff tick before the next player turn.</summary>
    public CombatEnemy TickDownDebuffs() =>
        this with {
            Vulnerable = Math.Max(0, Vulnerable - 1),
            Weak = Math.Max(0, Weak - 1),
        };

}
