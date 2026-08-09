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
        if (!AiSessionSettings.SyncBotSpawnPhantomPlayer) return false;
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return false;
        if (state.Players.Count != 1) return false;
        if (state.Players.Any(p => p.NetId == MpCheatSyncBot.PhantomPlayerNetId)) return false;
        if (state.CurrentMapPointHistoryEntry == null) return false;

        var host = state.Players[0];
        var character = AiSessionSettings.PhantomCharacter ?? host.Character;
        if (character == null) {
            KitLog.Warn("SyncBot", $"Phantom player spawn failed: no character selected.");
            return false;
        }

        var result = CompanionBridge.TrySummon(new CompanionSpawnRequest(
            character,
            PreferredNetId: MpCheatSyncBot.PhantomPlayerNetId,
            UnlockState: host.UnlockState));

        if (!result.Ok)
            KitLog.Warn("SyncBot", $"Phantom player spawn failed: {result.Error}");

        return result.Ok;
    }
}
