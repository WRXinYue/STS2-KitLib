using System.Linq;
using KitLib.Companion;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.SyncBot;

/// <summary>Spawns NetId 1001 after map history exists (not safe during RunManager.Launch postfix).</summary>
internal static class PhantomPlayerSpawner {
    public static bool TrySpawn(RunState? state) {
        if (state == null) return false;
        if (!SettingsStore.Current.SyncBotSpawnPhantomPlayer) return false;
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return false;
        if (state.Players.Count != 1) return false;
        if (state.Players.Any(p => p.NetId == MpCheatSyncBot.PhantomPlayerNetId)) return false;
        if (state.CurrentMapPointHistoryEntry == null) return false;

        var host = state.Players[0];
        if (host.Character == null) {
            MainFile.Logger.Warn("[SyncBot] Phantom player spawn failed: host character is null.");
            return false;
        }

        var result = CompanionBridge.TrySummon(new CompanionSpawnRequest(
            host.Character,
            PreferredNetId: MpCheatSyncBot.PhantomPlayerNetId,
            UnlockState: host.UnlockState));

        if (!result.Ok)
            MainFile.Logger.Warn($"[SyncBot] Phantom player spawn failed: {result.Error}");

        return result.Ok;
    }
}
