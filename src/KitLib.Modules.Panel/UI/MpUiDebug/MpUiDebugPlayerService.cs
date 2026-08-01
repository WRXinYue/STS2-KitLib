using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using KitLib.Actions;
using KitLib.Host;
using KitLib.Multiplayer.Cheat;
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

    internal static bool TryTeleportRestSiteFourSame() {
        if (!CanUse())
            return false;

        MpUiDebugState.PendingScenario = MpUiDebugScenario.RestSiteFourSame;
        if (!PreparePlayers(MpUiDebugState.RestSitePlayerCount, sameCharacterAsHost: true))
            return false;

        return RoomActions.TryEnterRoom(RoomType.RestSite);
    }

    internal static bool TryTeleportRelicSoloHand() {
        if (!CanUse())
            return false;

        MpUiDebugState.PendingScenario = MpUiDebugScenario.RelicSoloHand;
        if (!PreparePlayers(1, sameCharacterAsHost: false))
            return false;

        return RoomActions.TryEnterRoom(RoomType.Treasure);
    }

    internal static void ApplyPendingScenario(MpUiDebugScenario scenario) {
        if (MpUiDebugState.PendingScenario != scenario)
            return;

        MpUiDebugState.PendingScenario = MpUiDebugScenario.None;

        switch (scenario) {
            case MpUiDebugScenario.RestSiteFourSame:
                PreparePlayers(MpUiDebugState.RestSitePlayerCount, sameCharacterAsHost: true);
                break;
            case MpUiDebugScenario.RelicSoloHand:
                PreparePlayers(1, sameCharacterAsHost: false);
                break;
        }
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

        bool changed = false;

        while (players.Count > targetCount) {
            players.RemoveAt(players.Count - 1);
            changed = true;
        }

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
