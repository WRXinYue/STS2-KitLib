using System;
using KitLib.Map;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions;

internal static class RoomActions {
    public static bool IsRunInProgress => RunManager.Instance?.IsInProgress == true;

    /// <summary>
    /// Teleport into the given room type. In singleplayer (and fake-multiplayer) this calls the
    /// game's debug room entry API directly. In a real networked run it instead routes the teleport
    /// through the authoritative ActionQueue so every peer enters the room in lockstep.
    /// Requires an active run; silently fails (with a log warning) otherwise.
    /// </summary>
    public static bool TryEnterRoom(RoomType roomType) {
        try {
            var rm = RunManager.Instance;
            if (rm == null || !rm.IsInProgress) {
                KitLog.Warn($"TryEnterRoom: no run in progress.");
                return false;
            }

            if (roomType == RoomType.Map)
                MapScreenUnlock.EnableFromDevPanel();

            if (rm.IsSingleplayerOrFakeMultiplayer) {
                MapPointType pointType = roomType switch {
                    RoomType.Shop => MapPointType.Shop,
                    RoomType.RestSite => MapPointType.RestSite,
                    RoomType.Treasure => MapPointType.Treasure,
                    RoomType.Monster => MapPointType.Monster,
                    RoomType.Elite => MapPointType.Elite,
                    RoomType.Boss => MapPointType.Boss,
                    _ => MapPointType.Unassigned,
                };

                TaskHelper.RunSafely(rm.EnterRoomDebug(roomType, pointType));
                return true;
            }

            return EnqueueNetworkedRoom(roomType);
        }
        catch (Exception ex) {
            KitLog.Warn($"TryEnterRoom({roomType}) failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enqueue the teleport as an official networked console action ("room &lt;Type&gt;"). The game's
    /// <c>INetActionSubtypes</c> registry is a fixed list, so a custom net action can't be rebuilt on
    /// the wire; the official <see cref="ConsoleCmdGameAction"/> is the guaranteed-serializable carrier.
    /// On the host it is enqueued authoritatively and broadcast to all peers, so every end runs
    /// <c>EnterRoomDebug</c> in deterministic order.
    /// </summary>
    static bool EnqueueNetworkedRoom(RoomType roomType) {
        // Entering the Map screen via EnterRoomDebug does not balance the combat state
        // synchronizer, so a later teleport re-enters StartSync and desyncs (black screen).
        // The map screen is reached through normal navigation in multiplayer.
        if (roomType == RoomType.Map) {
            KitLog.Warn($"TryEnterRoom({roomType}): returning to the Map screen is unavailable for networked room teleport.");
            return false;
        }

        var rm = RunManager.Instance;
        if (rm == null) {
            KitLog.Warn($"TryEnterRoom({roomType}): missing run manager.");
            return false;
        }

        Player? me = LocalContext.GetMe(rm.DebugOnlyGetState());
        if (me == null) {
            KitLog.Warn($"TryEnterRoom({roomType}): cannot resolve local player for networked teleport.");
            return false;
        }

        string cmd = $"room {roomType}";
        bool inCombat = CombatManager.Instance?.IsInProgress == true;
        rm.ActionQueueSynchronizer.RequestEnqueue(new ConsoleCmdGameAction(me, cmd, inCombat));
        KitLog.Info("RoomActions", $"Enqueued networked room teleport: {cmd}.");
        return true;
    }
}
