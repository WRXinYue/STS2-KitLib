using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;
using DevMode.AI.Combat.Simulation;

namespace DevMode.AI.Combat;

/// <summary>Enemies that must never be lethal-fast-path targets.</summary>
internal static class LethalExclusions {
    public static bool ShouldSkip(JsonObject? enemy) =>
        EnemyMechanicResolver.IsIllusionMinion(enemy);

    public static bool ShouldSkip(CombatEnemy enemy) =>
        enemy.MechanicFlags.HasFlag(EnemyMechanicFlags.HasIllusionRevive);
}
