using System;
using System.Linq;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Multiplayer.SyncBot;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace KitLib.Companion;

internal static class CompanionSpawnService {
    internal static CompanionSpawnResult TrySpawn(CompanionSpawnRequest request) {
        if (request == null)
            return new(false, 0, "Request is required.");
        if (request.Character == null)
            return new(false, 0, "Character is required.");

        if (KitLibState.PseudoCoopDeferHeavyUi)
            return new(false, 0, "Run is still launching (pseudo-coop deferred UI).");

        var run = RunManager.Instance;
        if (run?.NetService?.Type != NetGameType.Host)
            return new(false, 0, "Companion spawn requires a multiplayer host run.");

        var state = run.DebugOnlyGetState();
        if (state == null)
            return new(false, 0, "No active run state.");

        if (state.CurrentMapPointHistoryEntry == null)
            return new(false, 0, "NotReadyYet: wait until after the first map point is visited.");

        if (!CompanionNetIdAllocator.TryAllocate(state, request.PreferredNetId, out var netId, out var allocError))
            return new(false, 0, allocError);

        if (state.Players.Any(p => p.NetId != (RunManager.Instance?.NetService?.NetId ?? 0)
                && p.Character?.Id == request.Character.Id)) {
            return new(false, netId, "That character is already a companion in this run.");
        }

        try {
            ApplyRuntimeSettings(request);

            var host = state.Players.FirstOrDefault(p => p.NetId == run.NetService!.NetId)
                ?? state.Players[0];
            var unlock = request.UnlockState
                ?? host.UnlockState
                ?? new UnlockState(SaveManager.Instance.Progress);

            var companion = Player.CreateForNewRun(request.Character, unlock, netId);
            state.AddPlayerDebug(companion, -1);

            MpCheatSyncBot.RefreshSimulatedPeers();
            SimulatedPeerRegistry.Refresh();

            run.MapSelectionSynchronizer?.OnLocationChanged(state.MapLocation);
            PseudoCoopLobbyRoster.RegisterSimulatedPeer(netId);
            PseudoCoopActionQueue.EnsureQueueForPlayer(companion);
            PseudoCoopMultiplayerUiRefresh.TryRefreshAfterPlayerJoined(state);

            if (request.Strategy != null)
                CompanionRegistry.Register(netId, request.Strategy);

            if (request.EnableNonCombatAi)
                CompanionNonCombatRegistry.Enable(netId);

            MainFile.Logger.Info(
                $"[Companion] Spawned netId={netId} character={request.Character.Id.Entry}.");

            return new(true, netId, null, companion);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[Companion] Spawn failed: {ex}");
            return new(false, netId, ex.Message);
        }
    }

    static void ApplyRuntimeSettings(CompanionSpawnRequest request) {
        var s = SettingsStore.Current;
        var changed = false;

        if (request.EnableAiTeammate && !s.MpAiTeammateEnabled) {
            s.MpAiTeammateEnabled = true;
            changed = true;
        }

        if (request.MirrorMapVotes && !s.SyncBotEnabled) {
            s.SyncBotEnabled = true;
            changed = true;
        }

        if (changed)
            SettingsStore.Save();

        MpCheatSession.TryArmSession("companion_spawn", allowWhileDeferredUi: true);
        SimulatedPeerRegistry.Refresh();
    }

    internal static bool TryDismissViaBridge(ulong netId) {
        if (netId == 0) return false;
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (netId == hostNetId) return false;
        CompanionRegistry.Unregister(netId);
        PseudoCoopLobbyRoster.UnregisterSimulatedPeer(netId);
        SimulatedPeerRegistry.Refresh();
        MainFile.Logger.Info($"[Companion] Dismissed netId={netId} (AI/roster only).");
        return true;
    }

    internal static IReadOnlyList<CompanionInfo> ListForBridge() {
        var state = RunManager.Instance?.DebugOnlyGetState();
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (state == null || hostNetId == 0)
            return [];
        return state.Players
            .Where(p => p.NetId != hostNetId)
            .Select(p => new CompanionInfo(
                p.NetId,
                p.Character!.Id,
                SimulatedPeerRegistry.IsHostDrivenPeer(p.NetId),
                p.Creature.IsAlive))
            .ToList();
    }
}
