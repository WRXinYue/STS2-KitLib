using System.Threading.Tasks;
using DevMode.AI.Core.Schema;

namespace DevMode.AI.Core;

/// <summary>
/// Abstraction for executing game actions.
/// Each game provides its own implementation that maps
/// <see cref="GameAction"/> to concrete game API calls.
/// </summary>
public interface IGameActionExecutor
{
    /// <summary>
    /// Execute the given action in the game.
    /// </summary>
    Task<ActionResult> ExecuteAsync(GameAction action);
}
