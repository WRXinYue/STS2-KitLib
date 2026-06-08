namespace KitLib.AI.Combat.Simulation;

public enum SimActionKind {
    PlayCard,
    UsePotion,
    EndTurn,
}

public sealed record SimCombatAction(
    SimActionKind Kind,
    int HandIndex = -1,
    int EnemyIndex = -1,
    int PotionSlot = -1,
    int McBranch = 0);
