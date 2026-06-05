namespace DevMode.AI.Combat.Simulation;

public enum SimActionKind {
    PlayCard,
    EndTurn,
}

public sealed record SimCombatAction(
    SimActionKind Kind,
    int HandIndex = -1,
    int EnemyIndex = -1);
