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
    CombatIntentStep[] IntentSteps) {
    public int EffectiveHp => CurrentHp + Block;

    public CombatEnemy WithHp(int hp, int block, bool alive) =>
        this with { CurrentHp = hp, Block = block, IsAlive = alive };

    public CombatEnemy WithIntent(int intentDamage) =>
        this with { IntentDamage = intentDamage };

    public CombatEnemy WithPowers(int vulnerable, int weak) =>
        this with { Vulnerable = vulnerable, Weak = weak };

    public CombatEnemy MarkDead() =>
        this with { CurrentHp = 0, Block = 0, IsAlive = false, IntentDamage = 0 };
}
