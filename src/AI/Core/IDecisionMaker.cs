using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DevMode.AI.Core.Schema;

namespace DevMode.AI.Core;

/// <summary>
/// Given a game snapshot and current phase, decide the next action.
/// Implementations can be rule-based, ML models, or external AI bridges.
/// </summary>
public interface IDecisionMaker
{
    /// <summary>
    /// Decide the next action to take.
    /// </summary>
    /// <param name="snapshot">Current game state snapshot (JSON, game-specific schema).</param>
    /// <param name="phase">Current game phase.</param>
    /// <returns>The action to execute.</returns>
    Task<GameAction> DecideAsync(JsonObject snapshot, GamePhase phase);
}
