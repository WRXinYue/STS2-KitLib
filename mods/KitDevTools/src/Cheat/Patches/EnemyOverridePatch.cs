using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Patches;

/// <summary>
/// Intercepts <see cref="ActModel.PullNextEncounter"/> to inject custom encounter overrides.
/// When a DevMode enemy override is active, the original encounter is replaced with the
/// user-selected one. Supports global, per-room-type, and per-floor overrides.
/// </summary>
[HarmonyPatch(typeof(ActModel), nameof(ActModel.PullNextEncounter))]
public static class EnemyOverridePatch {
    public static void Postfix(RoomType roomType, ref EncounterModel __result) {
        if (!KitLibState.CheatsInRun) return;

        int floor = RunManager.Instance?.DebugOnlyGetState()?.ActFloor ?? 0;
        var overrideEnc = KitLibState.ResolveOverride(roomType, floor);
        if (overrideEnc == null) return;

        // RunManager.CreateRoom uses PullNextEncounter(roomType).ToMutable() — the return here must be
        // canonical. Calling ToMutable() in this postfix produced a mutable instance and caused a
        // second ToMutable() in CreateRoom, triggering MutableModelException.
        __result = overrideEnc.IsCanonical
            ? overrideEnc
            : (overrideEnc.CanonicalInstance ?? ModelDb.GetById<EncounterModel>(((AbstractModel)overrideEnc).Id));
    }
}
