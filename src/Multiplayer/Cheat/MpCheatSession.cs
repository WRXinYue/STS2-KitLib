using KitLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat;

/// <summary>Runtime multiplayer cheat session (lobby + run scope).</summary>
public static class MpCheatSession {
    public static bool LocalOptIn { get; private set; }

    public static bool SessionArmed { get; private set; }

    public static string? LastBlockReason { get; private set; }

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

    public static ulong LocalNetId =>
        RunManager.Instance?.NetService?.NetId ?? 0;

    /// <summary>Net id for PerPlayer config keys — prefers run <see cref="Player.NetId"/> over net service.</summary>
    public static ulong ResolveLocalPlayerNetId() {
        if (RunContext.TryGetRunAndPlayer(out _, out var player) && player.NetId != 0)
            return player.NetId;
        return LocalNetId;
    }

    public static bool CanUseMultiplayerCheats =>
        InMultiplayerRun && SessionArmed && MpCheatState.IsActive;

    public static bool CanEditMultiplayerCheats =>
        CanUseMultiplayerCheats && IsHost;

    public static void SetLocalOptIn(bool enabled) {
        LocalOptIn = enabled;
        if (!enabled)
            Disarm("local_opt_out");
    }

    public static void TryArmSession(string reason, bool allowWhileDeferredUi = false) {
        LastBlockReason = null;
        if (!allowWhileDeferredUi && KitLibState.PseudoCoopDeferHeavyUi) {
            LastBlockReason = "pseudo_coop_deferred";
            return;
        }

        if (!InMultiplayerRun) {
            Disarm("not_multiplayer");
            return;
        }

        if (!LocalOptIn) {
            Disarm("local_opt_in_required");
            return;
        }

        MpCheatNetBus.TryRegisterHandlers();
        if (!MpCheatNetBus.IsReady) {
            Disarm("net_handlers_unavailable");
            MainFile.Logger.Warn("[MpCheat] NetMessage handlers unavailable; sync disabled.");
            return;
        }

        SessionArmed = true;
        MainFile.Logger.Info($"[MpCheat] Session armed ({reason}).");
    }

    public static void Disarm(string reason) {
        if (SessionArmed)
            MainFile.Logger.Info($"[MpCheat] Session disarmed: {reason}");
        SessionArmed = false;
        LastBlockReason = reason;
        MpCheatState.Clear();
    }

    public static void OnRunEnded() {
        Disarm("run_ended");
    }
}
