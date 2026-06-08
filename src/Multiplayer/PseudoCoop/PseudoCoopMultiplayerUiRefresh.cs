using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>
/// Phantom player (NetId 1001) is added after <see cref="NGlobalUi.Initialize"/>, so official UI
/// skips multiplayer player bar and map vote rings (both require Players.Count &gt; 1 at init).
/// </summary>
internal static class PseudoCoopMultiplayerUiRefresh {
    static readonly FieldInfo VoteAllPlayersField =
        AccessTools.Field(typeof(NMultiplayerVoteContainer), "_allPlayers")!;

    static readonly FieldInfo PlayerStateNodesField =
        AccessTools.Field(typeof(NMultiplayerPlayerStateContainer), "_nodes")!;

    static readonly FieldInfo MapPointDictionaryField =
        AccessTools.Field(typeof(NMapScreen), "_mapPointDictionary")!;

    public static void TryRefreshAfterPlayerJoined(RunState state) {
        if (state.Players.Count <= 1) return;
        Callable.From(() => RefreshDeferred(state)).CallDeferred();
    }

    public static bool NeedsMultiplayerUiRefresh(RunState state) {
        if (state.Players.Count <= 1) return false;

        var container = NRun.Instance?.GlobalUi?.MultiplayerPlayerContainer;
        if (container == null) return true;

        var mpNodes = container.GetChildren().Count(n => n is NMultiplayerPlayerState);
        if (mpNodes < state.Players.Count) return true;

        var map = NMapScreen.Instance;
        if (map == null) return false;

        if (MapPointDictionaryField.GetValue(map) is not Dictionary<MapCoord, NMapPoint> points
            || points.Count == 0)
            return false;

        var sample = points.Values.First().VoteContainer;
        if (VoteAllPlayersField.GetValue(sample) is List<MegaCrit.Sts2.Core.Entities.Players.Player> all)
            return all.Count < state.Players.Count;

        return true;
    }

    static void RefreshDeferred(RunState state) {
        if (!NeedsMultiplayerUiRefresh(state)) return;

        var globalUi = NRun.Instance?.GlobalUi;
        if (globalUi == null) return;

        RefreshMultiplayerPlayerBar(globalUi.MultiplayerPlayerContainer, state);
        RefreshMapVoteContainers(state);
        RunManager.Instance?.MapSelectionSynchronizer?.OnLocationChanged(state.MapLocation);
        globalUi.MultiplayerPlayerContainer.ShowImmediately();

        MainFile.Logger.Info(
            $"[PseudoCoop] Multiplayer UI refreshed ({state.Players.Count} players: "
            + $"{string.Join(", ", state.Players.Select(p => p.NetId))}).");
    }

    static void RefreshMultiplayerPlayerBar(NMultiplayerPlayerStateContainer container, RunState state) {
        foreach (var child in container.GetChildren().ToArray()) {
            if (child is NMultiplayerPlayerState)
                child.QueueFree();
        }

        if (PlayerStateNodesField.GetValue(container) is List<NMultiplayerPlayerState> nodes)
            nodes.Clear();

        container.Initialize(state);
    }

    static void RefreshMapVoteContainers(RunState state) {
        var map = NMapScreen.Instance;
        if (map == null) return;

        if (MapPointDictionaryField.GetValue(map) is not Dictionary<MapCoord, NMapPoint> points)
            return;

        foreach (var point in points.Values) {
            var vc = point.VoteContainer;
            if (VoteAllPlayersField.GetValue(vc) is List<MegaCrit.Sts2.Core.Entities.Players.Player> allPlayers) {
                allPlayers.Clear();
                allPlayers.AddRange(state.Players);
            }
            vc.RefreshPlayerVotes(animate: false);
        }

        map.RefreshAllMapPointVotes();
    }
}
