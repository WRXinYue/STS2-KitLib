using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;
using KitLib.Game;

namespace KitLib.AI.Combat;

public static class CombatSearch {
    public static GameAction? PickBestMove(JsonObject snapshot) =>
        CombatPlanner.PickBestMove(snapshot);
}
