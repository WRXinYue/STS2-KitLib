using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

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
    int ActOrder = 0) {
    public int EffectiveHp => CurrentHp + Block;

    /// <summary>HP damage this turn — debuff/summon pressure uses <see cref="NonDamageThreat"/> separately.</summary>
    public int EffectiveIncoming =>
        IsAlive ? IntentDamage + StrengthBonus() : 0;

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

    public CombatEnemy MarkDead() =>
        this with {
            CurrentHp = 0,
            Block = 0,
            IsAlive = false,
            IntentDamage = 0,
            NonDamageThreat = 0,
        };

    int StrengthBonus() {
        // Snapshot strength not yet wired into CombatEnemy; reserved for future.
        return 0;
    }
}
