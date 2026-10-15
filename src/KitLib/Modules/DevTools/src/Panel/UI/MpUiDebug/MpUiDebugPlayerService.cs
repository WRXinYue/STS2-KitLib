using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using KitLib.Actions;
using KitLib.Host;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.Play;
using KitLib.Multiplayer.PseudoCoop;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace KitLib.UI;

/// <summary>Prepares fake players and teleports into official MP layout test rooms.</summary>
internal static class MpUiDebugPlayerService {
    internal const ulong FirstDebugNetId = 9101;

    private static readonly AccessTools.FieldRef<RunState, List<Player>> PlayersRef =
        AccessTools.FieldRefAccess<RunState, List<Player>>("_players");

    private static bool _restoreScheduled;

    internal static bool TryTeleportRestSiteFourSame() {
        if (!CanUse())
            return false;

        MpUiDebugState.PendingScenario = MpUiDebugScenario.RestSiteFourSame;
        if (!PreparePlayers(MpUiDebugState.DebugPlayerCount, sameCharacterAsHost: true))
            return false;

        return RoomActions.TryEnterRoom(RoomType.RestSite);
    }

    internal static bool TryTeleportTreasureFourSame() {
        if (!CanUse())
            return false;

        MpUiDebugState.PendingScenario = MpUiDebugScenario.TreasureFourSame;
        if (!PreparePlayers(MpUiDebugState.DebugPlayerCount, sameCharacterAsHost: true))
            return false;

        return RoomActions.TryEnterRoom(RoomType.Treasure);
    }

    internal static void ApplyPendingScenario(MpUiDebugScenario scenario) {
        if (MpUiDebugState.PendingScenario != scenario)
            return;

        MpUiDebugState.PendingScenario = MpUiDebugScenario.None;

        switch (scenario) {
            case MpUiDebugScenario.RestSiteFourSame:
            case MpUiDebugScenario.TreasureFourSame:
                PreparePlayers(MpUiDebugState.DebugPlayerCount, sameCharacterAsHost: true);
                break;
        }
    }

    internal static void ScheduleRestoreAfterScenarioRoom() {
        if (!MpUiDebugState.HasSpawnedDebugPlayers)
            return;

        // Another mod-test teleport is switching rooms; keep players until the new room applies.
        if (MpUiDebugState.PendingScenario != MpUiDebugScenario.None)
            return;

        if (_restoreScheduled)
            return;

        _restoreScheduled = true;
        Callable.From(RestoreAfterScenarioRoomDeferred).CallDeferred();
    }

    private static void RestoreAfterScenarioRoomDeferred() {
        _restoreScheduled = false;

        if (!MpUiDebugState.HasSpawnedDebugPlayers)
            return;

        if (MpUiDebugState.PendingScenario != MpUiDebugScenario.None)
            return;

        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null)
            return;

        if (!RemoveMpUiDebugPlayers(state))
            return;

        MpUiDebugState.HasSpawnedDebugPlayers = false;
        MpUiDebugState.PendingMapVoteCleanup = true;
        CombatActionQueue.RemoveQueuesForMissingPlayers(state);
        PseudoCoopMultiplayerUiRefresh.RefreshAfterDebugPlayersRemoved(state);
        TryReinitializeSoloMapVotes();
    }

    internal static void TryReinitializeSoloMapVotes() {
        if (!MpUiDebugState.PendingMapVoteCleanup)
            return;

        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null || state.Players.Count > 1)
            return;

        if (!PseudoCoopMultiplayerUiRefresh.TryReinitializeSoloMapVotes(state))
            return;

        MpUiDebugState.PendingMapVoteCleanup = false;
    }

    private static bool CanUse() {
        if (!KitLibState.IsActive || KitLibState.PseudoCoopDeferHeavyUi)
            return false;

        var run = RunManager.Instance;
        return run?.IsInProgress == true && !MpCheatSession.InMultiplayerRun;
    }

    private static bool PreparePlayers(int targetCount, bool sameCharacterAsHost) {
        var run = RunManager.Instance;
        var state = run?.DebugOnlyGetState();
        if (state == null)
            return false;

        targetCount = Math.Max(1, targetCount);
        var players = PlayersRef(state);
        if (players.Count == 0)
            return false;

        var host = players[0];
        if (host.Character == null)
            return false;

        bool changed = RemoveMpUiDebugPlayers(state);

        var unlock = host.UnlockState ?? new UnlockState(SaveManager.Instance.Progress);
        var character = host.Character;

        while (players.Count < targetCount) {
            int slot = players.Count;
            var spawnCharacter = sameCharacterAsHost
                ? character
                : ResolveCharacter(slot) ?? character;
            ulong netId = AllocateNetId(state, slot - 1);

            try {
                var debugPlayer = Player.CreateForNewRun(spawnCharacter, unlock, netId);
                state.AddPlayerDebug(debugPlayer, -1);
                changed = true;
                MpUiDebugState.HasSpawnedDebugPlayers = true;
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"MpUiDebug: failed to spawn debug player slot {slot}: {ex.Message}");
                return false;
            }
        }

        if (changed)
            PseudoCoopMultiplayerUiRefresh.TryRefreshAfterPlayerJoined(state);

        return players.Count == targetCount;
    }

    private static bool RemoveMpUiDebugPlayers(RunState state) {
        var players = PlayersRef(state);
        bool changed = false;

        for (int i = players.Count - 1; i >= 1; i--) {
            if (!IsMpUiDebugPlayer(players[i]))
                continue;

            players.RemoveAt(i);
            changed = true;
        }

        return changed;
    }

    private static bool IsMpUiDebugPlayer(Player player) => player.NetId >= FirstDebugNetId;

    private static CharacterModel? ResolveCharacter(int slot) {
        string[] fallback = ["ironclad", "silent", "defect", "regent"];
        string id = slot < fallback.Length ? fallback[slot] : fallback[0];
        return ModelDb.AllCharacters.FirstOrDefault(c =>
            string.Equals(c.Id.Entry, id, StringComparison.OrdinalIgnoreCase));
    }

    private static ulong AllocateNetId(RunState state, int extraPlayerIndex) {
        ulong netId = FirstDebugNetId + (ulong)Math.Max(0, extraPlayerIndex);
        var players = PlayersRef(state);
        while (players.Any(p => p.NetId == netId))
            netId++;
        return netId;
    }
}
