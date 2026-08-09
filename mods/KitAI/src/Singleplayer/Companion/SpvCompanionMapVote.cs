using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Singleplayer.Companion;

/// <summary>Submits companion map votes via synchronizer (never local map UI clicks).</summary>
internal static class SpvCompanionMapVote {
    static readonly FieldInfo VotesField =
        AccessTools.Field(typeof(MapSelectionSynchronizer), "_votes")!;

    static readonly FieldInfo AcceptingVotesFromSourceField =
        AccessTools.Field(typeof(MapSelectionSynchronizer), "_acceptingVotesFromSource")!;

    internal static bool TryMirrorLocalVote(Player companion) {
        if (!SpvCompanionRegistry.IsSingleplayerRun())
            return false;

        var run = RunManager.Instance;
        var state = run?.DebugOnlyGetState();
        if (state == null)
            return false;

        var map = NMapScreen.Instance;
        if (map is not { IsOpen: true })
            return false;

        var sync = run!.MapSelectionSynchronizer;
        if (sync == null)
            return false;

        var local = LocalContext.GetMe(state.Players);
        if (local == null)
            return false;

        var localVote = sync.GetVote(local);
        if (!localVote.HasValue)
            return false;

        var companionVote = sync.GetVote(companion);
        if (companionVote.HasValue
            && companionVote.Value.coord == localVote.Value.coord
            && companionVote.Value.mapGenerationCount == localVote.Value.mapGenerationCount)
            return false;

        EnsureVoteCapacity(state, sync);
        var source = (MapLocation)AcceptingVotesFromSourceField.GetValue(sync)!;
        sync.PlayerVotedForMapCoord(companion, source, localVote.Value);
        map.RefreshAllMapPointVotes();

        return true;
    }

    static void EnsureVoteCapacity(RunState state, MapSelectionSynchronizer sync) {
        if (VotesField.GetValue(sync) is not List<MapVote?> votes)
            return;
        if (votes.Count >= state.Players.Count)
            return;

        sync.OnLocationChanged(state.MapLocation);
    }
}
