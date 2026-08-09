using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.AI.Sts2.Helpers;

/// <summary>
/// Frame-based condition waits for AI actions. Mirrors official AutoSlay
/// <c>WaitHelper.Until</c> but advances on <see cref="SceneTree.SignalName.ProcessFrame"/>.
/// </summary>
internal static class Sts2WaitHelper {
    public static async Task<bool> Until(Func<bool> condition, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline) {
            if (condition())
                return true;

            if (NGame.Instance != null)
                await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
            else
                await Task.Delay(100);
        }

        return condition();
    }

    public static Task<bool> ActionsSettled(TimeSpan timeout) =>
        Until(ArePlayerDrivenActionsSettled, timeout);

    internal static bool ArePlayerDrivenActionsSettled() {
        var running = RunManager.Instance.ActionExecutor.CurrentlyRunningAction;
        if (running != null && ActionQueueSet.IsGameActionPlayerDriven(running))
            return false;

        var ready = RunManager.Instance.ActionQueueSet.GetReadyAction();
        if (ready != null && ActionQueueSet.IsGameActionPlayerDriven(ready))
            return false;

        return true;
    }
}
