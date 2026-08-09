using System;
using System.Linq;
using KitLib.AI.Core;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Multiplayer.SyncBot;
using KitLib.Settings;
using KitLib.Singleplayer.Companion;
using MegaCrit.Sts2.Core.Combat;
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

        var run = RunManager.Instance;
        if (run?.IsInProgress != true)
            return new(false, 0, "No active run.");

        return run.NetService?.Type switch {
            NetGameType.Singleplayer => TrySpawnSingleplayer(request),
            NetGameType.Host => TrySpawnMultiplayerHost(request),
            _ => new(false, 0, "Companion spawn requires singleplayer or multiplayer host."),
        };
    }

    static CompanionSpawnResult TrySpawnSingleplayer(CompanionSpawnRequest request) {
        var run = RunManager.Instance!;
        var state = run.DebugOnlyGetState();
        if (state == null)
            return new(false, 0, "No active run state.");

        if (state.CurrentMapPointHistoryEntry == null)
            return new(false, 0, "NotReadyYet: wait until after the first map point is visited.");

        if (!CompanionNetIdAllocator.TryAllocate(state, request.PreferredNetId, out var netId, out var allocError))
            return new(false, 0, allocError);

        var localNetId = SpvCompanionRegistry.GetLocalNetId(state);
        if (state.Players.Any(p => p.NetId != localNetId && p.Character?.Id == request.Character.Id))
            return new(false, netId, "That character is already a companion in this run.");

        try {
            var local = state.Players.FirstOrDefault(p => p.NetId == localNetId) ?? state.Players[0];
            var unlock = request.UnlockState
                ?? local.UnlockState
                ?? new UnlockState(SaveManager.Instance.Progress);

            var companion = Player.CreateForNewRun(request.Character, unlock, netId);
            state.AddPlayerDebug(companion, -1);

            SpvCompanionRegistry.Register(netId);
            SpvCompanionSession.OnCompanionSpawned();

            PseudoCoopMultiplayerUiRefresh.TryRefreshAfterPlayerJoined(state);
            SpvCompanionCombatJoin.TryJoinActiveCombat(companion);
            PseudoCoopActionQueue.EnsureQueueForPlayer(companion);
            run.MapSelectionSynchronizer?.OnLocationChanged(state.MapLocation);

            if (request.Strategy is IDecisionMaker strategy)
                CompanionRegistry.Register(netId, strategy);

            // SP companions default to full AI (rewards, events, shop); MP still uses EnableNonCombatAi.
            CompanionNonCombatRegistry.Enable(netId);

            KitLog.Info("Companion", $"Spawned SP companion netId={netId} character={request.Character.Id.Entry}.");

            return new(true, netId, null, companion);
        }
        catch (Exception ex) {
            KitLog.Warn("Companion", $"SP spawn failed: {ex}");
            return new(false, netId, ex.Message);
        }
    }

    static CompanionSpawnResult TrySpawnMultiplayerHost(CompanionSpawnRequest request) {
        var isPhantomSpawn = request.PreferredNetId == MpCheatSyncBot.PhantomPlayerNetId;
        if (KitLibState.PseudoCoopDeferHeavyUi && !isPhantomSpawn)
            return new(false, 0, "Run is still launching (pseudo-coop deferred UI).");

        var run = RunManager.Instance!;
        var state = run.DebugOnlyGetState();
        if (state == null)
            return new(false, 0, "No active run state.");

        if (state.CurrentMapPointHistoryEntry == null)
            return new(false, 0, "NotReadyYet: wait until after the first map point is visited.");

        if (!CompanionNetIdAllocator.TryAllocate(state, request.PreferredNetId, out var netId, out var allocError))
            return new(false, 0, allocError);

        if (state.Players.Any(p => p.NetId != (run.NetService?.NetId ?? 0)
                && p.Character?.Id == request.Character.Id)) {
            return new(false, netId, "That character is already a companion in this run.");
        }

        try {
            ApplyMultiplayerRuntimeSettings(request);

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

            if (request.Strategy is IDecisionMaker strategy)
                CompanionRegistry.Register(netId, strategy);

            if (request.EnableNonCombatAi)
                CompanionNonCombatRegistry.Enable(netId);

            KitLog.Info("Companion", $"Spawned MP companion netId={netId} character={request.Character.Id.Entry}.");

            return new(true, netId, null, companion);
        }
        catch (Exception ex) {
            KitLog.Warn("Companion", $"MP spawn failed: {ex}");
            return new(false, netId, ex.Message);
        }
    }

    static void ApplyMultiplayerRuntimeSettings(CompanionSpawnRequest request) {
        if (request.EnableAiTeammate)
            AiSessionSettings.MpAiTeammateEnabled = true;

        if (request.MirrorMapVotes)
            AiSessionSettings.SyncBotEnabled = true;

        MpCheatSession.TryArmSession("companion_spawn", allowWhileDeferredUi: true);
        SimulatedPeerRegistry.Refresh();
    }

    internal static bool TryDismissViaBridge(ulong netId) {
        if (netId == 0) return false;

        var run = RunManager.Instance;
        var state = run?.DebugOnlyGetState();
        if (state == null) return false;

        var localNetId = run!.NetService?.Type == NetGameType.Singleplayer
            ? SpvCompanionRegistry.GetLocalNetId(state)
            : run.NetService?.NetId ?? 0;
        if (netId == localNetId) return false;

        CompanionRegistry.Unregister(netId);
        CompanionNonCombatRegistry.Disable(netId);

        if (SpvCompanionRegistry.IsCompanion(netId)) {
            SpvCompanionRegistry.Unregister(netId);
            KitLog.Info("Companion", $"Dismissed SP companion netId={netId}.");
            return true;
        }

        PseudoCoopLobbyRoster.UnregisterSimulatedPeer(netId);
        SimulatedPeerRegistry.Refresh();
        KitLog.Info("Companion", $"Dismissed MP companion netId={netId}.");
        return true;
    }

    internal static IReadOnlyList<CompanionInfo> ListForBridge() {
        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) return [];

        var run = RunManager.Instance!;
        var localNetId = run.NetService?.Type == NetGameType.Singleplayer
            ? SpvCompanionRegistry.GetLocalNetId(state)
            : run.NetService?.NetId ?? 0;

        return state.Players
            .Where(p => p.NetId != localNetId)
            .Select(p => new CompanionInfo(
                p.NetId,
                p.Character!.Id,
                SpvCompanionRegistry.IsAiDriven(p.NetId)
                    || SimulatedPeerRegistry.IsHostDrivenPeer(p.NetId),
                p.Creature.IsAlive))
            .ToList();
    }
}
