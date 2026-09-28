using System;
using KitLib.Models;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions;

internal static class TestServerActions {
    /// <summary>Teleports the run into a KitLib-owned card test combat (<see cref="KitLibTestServerEncounter"/>).</summary>
    internal static bool TryEnterTestRoom() {
        try {
            var rm = RunManager.Instance;
            if (rm == null || !rm.IsInProgress) {
                MainFile.Logger.Warn("CardTestActions: No run in progress for test room.");
                return false;
            }
            if (MpCheatSession.InMultiplayerRun) {
                MainFile.Logger.Warn("CardTestActions: Test room not available in multiplayer.");
                return false;
            }
            var encounter = ModelDb.Encounter<KitLibTestServerEncounter>().ToMutable();
            TaskHelper.RunSafely(rm.EnterRoomDebug(RoomType.Monster, MapPointType.Monster, encounter));
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"CardTestActions.TryEnterTestRoom failed: {ex.Message}");
            return false;
        }
    }
}
