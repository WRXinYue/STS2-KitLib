using KitLib.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
namespace KitLib.Multiplayer.PseudoCoop.Patches;

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
internal static class PseudoCoopMapFinishPatch {
    static void Postfix() {
        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state != null && SettingsStore.Current.SyncBotSpawnPhantomPlayer)
            PseudoCoopMultiplayerUiRefresh.TryRefreshAfterPlayerJoined(state);

        if (!KitLibState.PseudoCoopAwaitingMapFinish) return;
        PseudoCoopDeferredInit.TryScheduleMapFinish();
    }
}
