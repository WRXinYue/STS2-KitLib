namespace KitLib.Multiplayer.Cheat;

/// <summary>Authoritative in-run multiplayer cheat config (not per-frame).</summary>
public static class MpCheatState {
    private static MpCheatConfig _config = new();
    private static long _revision;

    public static long Revision => _revision;

    public static bool IsActive => _config.SessionEnabled;

    public static MpCheatConfig Config => _config;

    public static void ApplySnapshot(MpCheatConfig config, long revision, string reason) {
        _config = config;
        _revision = revision;
        var localNetId = MpCheatSession.ResolveLocalPlayerNetId();
        if (MpCheatSession.InMultiplayerRun) {
            _config.NormalizePerPlayerKeys(localNetId);
            _config.ApplyToLocalKitLibState(localNetId);
            LogPerPlayerState(revision, reason, localNetId);
        }
        else {
            _config.ApplyToKitLibState();
            MainFile.Logger.Info($"[MpCheat] Applied config rev={revision} ({reason}).");
        }
    }

    /// <summary>Apply local KitLibState to in-memory config before host round-trip (client UI toggles).</summary>
    public static void ApplyOptimisticFromKitLibState() {
        if (!MpCheatSession.InMultiplayerRun) return;
        var netId = MpCheatSession.ResolveLocalPlayerNetId();
        if (netId == 0) {
            MainFile.Logger.Warn("[MpCheat] Optimistic config skipped: local player net id is 0.");
            return;
        }
        _config = MpCheatConfig.MergeLocalEdits(_config, netId, MpCheatSession.IsHost);
        _config.SessionEnabled = true;
        MainFile.Logger.Info(
            $"[MpCheat] Optimistic per-player config applied netId={netId} infiniteBlock={KitLibState.PlayerCheats.InfiniteBlock}.");
    }

    private static void LogPerPlayerState(long revision, string reason, ulong localNetId) {
        _config.TryGetPlayerFlags(localNetId, out var local);
        var keys = string.Join(",", _config.PerPlayer.Keys);
        MainFile.Logger.Info(
            $"[MpCheat] Applied config rev={revision} ({reason}) localNetId={localNetId} perPlayerKeys=[{keys}] "
            + $"localInfiniteBlock={local.InfiniteBlock} sessionEnabled={_config.SessionEnabled}");
    }

    public static void Clear() {
        _config = new MpCheatConfig();
        _revision = 0;
    }
}
