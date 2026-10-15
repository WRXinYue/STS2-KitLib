using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.AutoSlay;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

internal static class AutoSlayRunner {
    static AutoSlayer? _instance;

    internal static bool SuppressQuit { get; private set; }
    internal static bool IsRunning => AutoSlayer.IsActive;
    internal static string? LastSeed { get; private set; }
    internal static string DraftSeed { get; set; } = "";

    internal static bool IsBlockedByMultiplayer {
        get {
            if (MpCheatSession.InMultiplayerRun)
                return true;
            var type = RunManager.Instance?.NetService?.Type ?? NetGameType.None;
            return type is NetGameType.Host or NetGameType.Client;
        }
    }

    internal static bool TryStart(string? seed, out string message) {
        if (IsBlockedByMultiplayer) {
            message = I18N.T("autoslay.mpWarning", "Not available in multiplayer.");
            return false;
        }

        if (AutoSlayer.IsActive) {
            message = I18N.T("autoslay.alreadyRunning", "Already running.");
            return false;
        }

        var trimmed = seed?.Trim() ?? "";
        DraftSeed = trimmed;
        var resolved = string.IsNullOrEmpty(trimmed)
            ? SeedHelper.GetRandomSeed()
            : SeedHelper.CanonicalizeSeed(trimmed);

        SuppressQuit = true;
        LastSeed = resolved;
        _instance = new AutoSlayer();
        _instance.Start(resolved);
        MainFile.Logger.Info($"AutoSlay started from main menu seed={resolved}");
        message = I18N.T("autoslay.started", "Started with seed {0}", resolved);
        return true;
    }

    internal static void Stop() {
        _instance?.Stop();
        _instance = null;
    }
}
