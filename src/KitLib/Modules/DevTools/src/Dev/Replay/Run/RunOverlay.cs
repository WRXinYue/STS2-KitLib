using Godot;
using MegaCrit.Sts2.Core.Nodes;

namespace KitLib.Replay;

/// <summary>Hooks RunReplays' overlay call sites to the KitDevTools CS bar.</summary>
internal static class RunOverlay {
    internal static void InitForRun() {
        if (!ReplayEngine.IsReplayRun)
            return;
        var tree = NGame.Instance?.GetTree();
        if (tree != null)
            CombatReplayPlayback.ShowHud?.Invoke(tree);
    }

    internal static void RestoreRecentEntries(IEnumerable<string> _) { }

    internal static void NotifyCardPlayFinished() { }

    internal static void HideForMainMenu() {
        CombatReplayPlayback.HideHud?.Invoke();
    }
}
