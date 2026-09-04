using KitLib.Game;

namespace KitLib.AI.Core;

public interface IGameActionExecutor {
    Task<ActionResult> ExecuteAsync(GameAction action);
}
