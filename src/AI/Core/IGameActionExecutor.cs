using System.Threading.Tasks;
using KitLib.AI.Core.Schema;

namespace KitLib.AI.Core;

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
