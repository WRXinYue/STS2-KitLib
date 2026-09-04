using System.Text.Json.Nodes;
using KitLib.Game;

namespace KitLib.AI.Core;

public interface IDecisionMaker {
    Task<GameAction> DecideAsync(JsonObject snapshot, GamePhase phase);
}
