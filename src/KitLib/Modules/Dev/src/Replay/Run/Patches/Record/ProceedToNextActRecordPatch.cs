using HarmonyLib;
using KitLib.Replay.Commands;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace KitLib.Replay.Patches.Record;

/// <summary>
/// Records ProceedToNextAct when the player signals readiness to change acts.
/// </summary>
[HarmonyPatch(typeof(ActChangeSynchronizer), nameof(ActChangeSynchronizer.SetLocalPlayerReady))]
public static class ProceedToNextActRecordPatch {
    [HarmonyPrefix]
    public static void Prefix() {
        if (ReplayEngine.IsActive) return;

        PlayerActionBuffer.Record(new ProceedToNextActCommand().ToString()!);
    }
}
