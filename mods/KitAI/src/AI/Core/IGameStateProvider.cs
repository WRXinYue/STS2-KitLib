using System.Text.Json.Nodes;
using KitLib.Game;

namespace KitLib.AI.Core;

public interface IGameStateProvider {
    bool IsRunActive { get; }
    GamePhase CurrentPhase { get; }
    JsonObject? TakeSnapshot();
}
