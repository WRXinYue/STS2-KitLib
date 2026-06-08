using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KitLib.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>
/// Mirrors host map vote to phantom/simulated peers.
/// Official flow: <see cref="NMapScreen.OnMapPointSelectedLocally"/> → <see cref="VoteForMapCoordAction"/>
/// → <see cref="MapSelectionSynchronizer.PlayerVotedForMapCoord"/> (all players must vote) → <c>MoveToMapCoord</c>.
/// </summary>
internal static class PseudoCoopMapVoteMirror {
    static bool _mirroring;

    static readonly FieldInfo VotesField =
        AccessTools.Field(typeof(MapSelectionSynchronizer), "_votes")!;

    static readonly FieldInfo AcceptingVotesFromSourceField =
        AccessTools.Field(typeof(MapSelectionSynchronizer), "_acceptingVotesFromSource")!;

    internal static bool ShouldMirror {
        get {
            var s = SettingsStore.Current;
            if (s.MpAiTeammateEnabled || s.SyncBotEnabled) return true;
            return s.SyncBotSpawnPhantomPlayer
                && RunManager.Instance?.NetService?.Type == NetGameType.Host;
        }
    }

    /// <summary>Mirror after host vote; <paramref name="source"/> must match synchronizer's accepting source.</summary>
    internal static void MirrorHostVote(NMapScreen mapScreen, MapLocation source, MapVote destination) {
        if (!ShouldMirror || _mirroring) return;

        _mirroring = true;
        try {
            var run = RunManager.Instance;
            if (run?.NetService?.Type != NetGameType.Host) return;

            var state = run.DebugOnlyGetState();
            if (state == null || state.Players.Count <= 1) return;

            var sync = run.MapSelectionSynchronizer;
            EnsureVoteCapacity(state, sync);

            // Official rejects votes when source != _acceptingVotesFromSource (see MapSelectionSynchronizer.cs:50).
            var accepting = (MapLocation)AcceptingVotesFromSourceField.GetValue(sync)!;
            if (accepting != source) {
                MainFile.Logger.Warn(
                    $"[PseudoCoop] Map vote source mismatch: vote={source} accepting={accepting}; using accepting.");
                source = accepting;
            }

            var peers = SimulatedPeerRegistry.GetMapMirrorTargets().ToList();
            if (peers.Count == 0) {
                MainFile.Logger.Warn(
                    $"[PseudoCoop] No simulated peers to mirror {destination.coord} "
                    + $"(players={state.Players.Count}, votes={VoteCount(sync)}, hostNetId={run.NetService.NetId}).");
                return;
            }

            foreach (var peer in peers) {
                var existing = sync.GetVote(peer);
                if (existing.HasValue
                    && existing.Value.coord == destination.coord
                    && existing.Value.mapGenerationCount == destination.mapGenerationCount)
                    continue;

                sync.PlayerVotedForMapCoord(peer, source, destination);
                MainFile.Logger.Info($"[PseudoCoop] Auto map vote netId={peer.NetId} -> {destination.coord}");
            }

            mapScreen.RefreshAllMapPointVotes();
        }
        catch (System.Exception ex) {
            MainFile.Logger.Warn($"[PseudoCoop] Map vote mirror failed: {ex}");
        }
        finally {
            _mirroring = false;
        }
    }

    internal static void MirrorHostVote(NMapScreen mapScreen, MapVote destination) {
        var run = RunManager.Instance;
        var sync = run?.MapSelectionSynchronizer;
        if (sync == null) return;
        var source = (MapLocation)AcceptingVotesFromSourceField.GetValue(sync)!;
        MirrorHostVote(mapScreen, source, destination);
    }

    internal static void MirrorHostVote(NMapScreen mapScreen, MapCoord coord) {
        var run = RunManager.Instance;
        var state = run?.DebugOnlyGetState();
        if (run == null || state == null) return;

        var sync = run.MapSelectionSynchronizer;
        var destination = new MapVote {
            coord = coord,
            mapGenerationCount = sync.MapGenerationCount,
        };
        MirrorHostVote(mapScreen, destination);
    }

    static void EnsureVoteCapacity(RunState state, MapSelectionSynchronizer sync) {
        if (VotesField.GetValue(sync) is not List<MapVote?> votes) return;
        if (votes.Count >= state.Players.Count) return;

        sync.OnLocationChanged(state.MapLocation);
        MainFile.Logger.Info(
            $"[PseudoCoop] Map vote slots expanded {votes.Count} -> {state.Players.Count}.");
    }

    static int VoteCount(MapSelectionSynchronizer sync) =>
        VotesField.GetValue(sync) is List<MapVote?> votes ? votes.Count : 0;
}
