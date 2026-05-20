using System.Text.Json.Nodes;
using DevMode.AI.Core.Schema;

namespace DevMode.AI.Core;

/// <summary>
/// Abstraction for reading the current game state.
/// Each game provides its own implementation.
/// </summary>
public interface IGameStateProvider
{
    /// <summary>Whether a run is currently in progress.</summary>
    bool IsRunActive { get; }

    /// <summary>The current game phase / screen.</summary>
    GamePhase CurrentPhase { get; }

    /// <summary>
    /// Capture a full snapshot of the game state as a JSON object.
    /// The schema is game-specific; consumers should treat it as opaque data
    /// unless they know the concrete game implementation.
    /// </summary>
    JsonObject? TakeSnapshot();
}
