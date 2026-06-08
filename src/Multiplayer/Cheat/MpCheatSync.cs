using KitLib;
using KitLib.Settings;

namespace KitLib.Multiplayer.Cheat;

/// <summary>Initializes multiplayer cheat sync (INetMessage + run lifecycle).</summary>
public static class MpCheatSync {
    public static void Initialize() {
        MpCheatSession.SetLocalOptIn(SettingsStore.Current.MultiplayerCheatOptIn);
    }

    public static void OnRunStarted() {
        if (KitLibState.PseudoCoopDeferHeavyUi) return;
        if (!MpCheatSession.InMultiplayerRun) return;

        MpCheatSession.TryArmSession("run_started");
        if (!MpCheatSession.SessionArmed) return;

        MpCheatNetBus.TryRegisterHandlers();
        // Initial config publish runs from NRun._Ready (nrun_ready) to keep Launch→SetCurrentScene light.
    }

    public static void OnRunEnded() {
        MpCheatNetBus.Reset();
        MpCheatSession.OnRunEnded();
    }

    public static void HostPublishFromKitLibState(string reason) {
        if (!MpCheatSession.CanEditMultiplayerCheats) return;
        var netId = MpCheatSession.ResolveLocalPlayerNetId();
        if (netId == 0) {
            MainFile.Logger.Warn($"[MpCheat] Host publish skipped ({reason}): local player net id is 0.");
            return;
        }
        var config = MpCheatConfig.MergeLocalEdits(MpCheatState.Config, netId, includeSharedGlobals: true);
        MpCheatNetBus.HostPublishConfig(config, reason);
    }

    internal static void TryPublishInitialHostConfig(string reason) {
        if (KitLibState.PseudoCoopDeferHeavyUi || KitLibState.PseudoCoopDeferMpCheatPublish) return;
        if (!MpCheatSession.IsHost || !MpCheatSession.SessionArmed) return;
        if (MpCheatState.Revision > 0 && reason != "run_start") return;
        var netId = MpCheatSession.ResolveLocalPlayerNetId();
        if (netId == 0) {
            MainFile.Logger.Debug($"[MpCheat] Initial host config deferred ({reason}): net id not ready.");
            return;
        }
        var config = MpCheatConfig.MergeLocalEdits(MpCheatState.Config, netId, includeSharedGlobals: true);
        MpCheatNetBus.HostPublishConfig(config, reason);
    }

    public static void BroadcastCommand(MpCheatCommandMessage message) =>
        MpCheatNetBus.BroadcastCommand(message);

    public static void TryPersistConfig(MpCheatConfig config) =>
        MpCheatRunSavedData.TryWrite(config);
}
