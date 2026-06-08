using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KitLib.Multiplayer.SyncBot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

/// <summary>Injects sync payloads for simulated peers (e.g. phantom 1001) so WaitForSync does not hang.</summary>
[HarmonyPatch(typeof(CombatStateSynchronizer), nameof(CombatStateSynchronizer.StartSync))]
internal static class PseudoCoopCombatSyncPatch {
    static readonly FieldInfo SyncDataField =
        AccessTools.Field(typeof(CombatStateSynchronizer), "_syncData")!;

    static readonly FieldInfo SyncCompletionSourceField =
        AccessTools.Field(typeof(CombatStateSynchronizer), "_syncCompletionSource")!;

    static readonly MethodInfo CheckSyncCompletedMethod =
        AccessTools.Method(typeof(CombatStateSynchronizer), "CheckSyncCompleted")!;

    [HarmonyPostfix]
    static void Postfix(CombatStateSynchronizer __instance) {
        if (__instance.IsDisabled) return;
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return;

        var state = RunManager.Instance.DebugOnlyGetState();
        if (state == null) return;

        var syncData = (Dictionary<ulong, SerializablePlayer>)SyncDataField.GetValue(__instance)!;
        var injected = false;

        // Prefer simulated roster; fall back to all remote run peers (phantom may be in RunLobby ids).
        var peerIds = SimulatedPeerRegistry.GetSimulatedPeerNetIds();
        if (peerIds.Count == 0)
            peerIds = SimulatedPeerRegistry.GetRemoteRunNetIds();

        foreach (var netId in peerIds) {
            if (syncData.ContainsKey(netId)) continue;
            if (SimulatedPeerRegistry.IsLiveEnetPeer(netId)) continue;

            var player = state.GetPlayer(netId);
            syncData[netId] = player.ToSerializable();
            injected = true;
            MainFile.Logger.Info($"[PseudoCoop] Injected combat sync for simulated peer netId={netId}.");
        }

        // StartSync already calls CheckSyncCompleted; solo pseudo-coop lobby may complete
        // before phantom injection — calling again throws on TaskCompletionSource.SetResult.
        if (!injected) return;

        if (SyncCompletionSourceField.GetValue(__instance) is not System.Threading.Tasks.TaskCompletionSource tcs
            || tcs.Task.IsCompleted)
            return;

        CheckSyncCompletedMethod.Invoke(__instance, null);
    }
}
