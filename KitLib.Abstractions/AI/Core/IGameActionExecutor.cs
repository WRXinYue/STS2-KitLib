using KitLib.AI.Core.Schema;

namespace KitLib.AI.Core;

public interface IGameActionExecutor {
    Task<ActionResult> ExecuteAsync(GameAction action);
}
