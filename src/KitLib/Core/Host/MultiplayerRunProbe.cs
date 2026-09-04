using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Host;

/// <summary>Read-only multiplayer run detection for modules that cannot reference KitLib.Cheat.</summary>
public static class MultiplayerRunProbe {
    public static bool InMultiplayerRun {
        get {
            var run = RunManager.Instance;
            if (run?.IsInProgress != true) return false;
            var type = run.NetService?.Type ?? NetGameType.None;
            return type is NetGameType.Host or NetGameType.Client;
        }
    }

    public static bool IsHost =>
        RunManager.Instance?.NetService?.Type == NetGameType.Host;
}
