using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI.Core.Schema;
using KitLib.Game;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Host;

/// <summary>Player-facing game I/O: lean snapshot, UI execution, and card-selection screens.</summary>
public static class KitLibGameOps {
    static readonly PlayerActionExecutor Executor = new(msg => KitLog.Info("GameOps", msg));

    public static JsonObject? Snapshot() {
        if (!RunContext.TryGetRunAndPlayer(out var state, out var player) || player == null)
            return null;
        return LeanGameSnapshot.Capture(state, player, LeanGamePhase.Current());
    }

    public static JsonObject? CaptureCombatAfterState() {
        if (!RunContext.TryGetRunAndPlayer(out _, out var player) || player == null)
            return null;
        return LeanGameSnapshot.CaptureCombatAfter(player);
    }

    public static Task<ActionResult> Execute(GameAction action, SelectionHint? hint = null) =>
        GameMainThread.InvokeAsync(() => ExecuteCore(action, hint));

    internal static Task<ActionResult> ExecuteFor(
        RunState state,
        Player player,
        GameAction action,
        SelectionHint? hint = null) =>
        GameMainThread.InvokeAsync(() => Executor.ExecuteAsync(state, player, action, hint));

    public static JsonObject SelectionState() => CardSelectionUi.CaptureState();

    public static Task<JsonObject> PickSelection(JsonObject args) =>
        GameMainThread.InvokeAsync(() => CardSelectionUi.PickAsync(args));

    static async Task<ActionResult> ExecuteCore(GameAction action, SelectionHint? hint) {
        if (!RunContext.TryGetRunAndPlayer(out var state, out var player) || player == null)
            return ActionResult.Fail("No active run or player.");
        return await Executor.ExecuteAsync(state, player, action, hint);
    }
}
