using System.Text.Json.Nodes;
using DevMode.AI.Combat.Simulation;
using DevMode.AI.Core.Schema;

namespace DevMode.AI.Combat;

public static class CombatSearch {
    public static GameAction? PickBestMove(JsonObject snapshot) =>
        CombatPlanner.PickBestMove(snapshot);
}
