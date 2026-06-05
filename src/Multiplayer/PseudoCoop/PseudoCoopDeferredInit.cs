using DevMode;
using DevMode.Multiplayer.Cheat;
using DevMode.Multiplayer.LanTest;
using DevMode.Patches;
using DevMode.UI;
using Godot;
using MegaCrit.Sts2.Core.Nodes;

namespace DevMode.Multiplayer.PseudoCoop;

/// <summary>Finishes pseudo-coop launch: MpCheat arm after EnterAct; DevPanel/publish after map opens.</summary>
internal static class PseudoCoopDeferredInit {
    public static void TryScheduleAfterEnterAct0() {
        if (!DevModeState.PseudoCoopLaunchPending) return;

        DevModeState.PseudoCoopLaunchPending = false;
        MainFile.Logger.Info("[PseudoCoop] Scheduling deferred init (next frame)…");
        Callable.From(CompleteDeferred).CallDeferred();
    }

    static void CompleteDeferred() {
        GlobalUiReadyPatch.EnsureProcessNodeOnly(NRun.Instance?.GlobalUi);
        MainFile.Logger.Info("[PseudoCoop] Deferred init complete (MpCheat arm + DevPanel on map open).");
    }

    internal static void TryScheduleMapFinish() {
        if (!DevModeState.PseudoCoopAwaitingMapFinish) return;

        var run = NRun.Instance;
        if (run == null) {
            RunLateMpCheatArm();
            RunLateMpCheatPublish();
            RunLateDevPanel();
            DevModeState.PseudoCoopAwaitingMapFinish = false;
            return;
        }

        if (run.GetNodeOrNull<PseudoCoopMapFinishNode>("PseudoCoopMapFinish") != null)
            return;

        run.AddChild(new PseudoCoopMapFinishNode { Name = "PseudoCoopMapFinish" });
        MainFile.Logger.Info("[PseudoCoop] Map finish scheduled (DevPanel + publish after map open).");
    }

    internal static void RunLateDevPanel() {
        DevModeState.PseudoCoopDeferHeavyUi = false;
        var globalUi = NRun.Instance?.GlobalUi;
        if (DevModeInstanceRegistry.IsDualInstanceActive()) {
            MainFile.Logger.Info("[PseudoCoop] Map finish: minimal DevPanel (AI Host)…");
            GlobalUiReadyPatch.TryAttachDualInstanceMinimal(globalUi);
        }
        else {
            MainFile.Logger.Info("[PseudoCoop] Map finish: DevPanel…");
            GlobalUiReadyPatch.TryAttachDeferred(globalUi, skipWarmup: true);
        }

        if (DevPanelUI.IsRailAttached)
            MainFile.Logger.Info("[PseudoCoop] DevPanel attached.");
        else
            MainFile.Logger.Warn("[PseudoCoop] DevPanel attach skipped (DevMode inactive or UI unavailable).");

        if (DevModeInstanceRegistry.IsDualInstanceActive())
            DualInstanceTestBootstrap.TryAutoLanPresetsOnLaunch();
    }

    internal static void RunLateMpCheatArm() {
        if (!MpCheatSession.LocalOptIn) {
            MainFile.Logger.Info("[PseudoCoop] MpCheat arm skipped (no opt-in).");
            return;
        }

        MpCheatSession.TryArmSession("map_finish", allowWhileDeferredUi: true);
        if (!MpCheatSession.SessionArmed) {
            MainFile.Logger.Warn(
                $"[PseudoCoop] MpCheat arm failed: {MpCheatSession.LastBlockReason ?? "unknown"}.");
            return;
        }

        MainFile.Logger.Info("[PseudoCoop] MpCheat armed (publish deferred until map).");
    }

    internal static void RunLateMpCheatPublish() {
        if (!MpCheatSession.LocalOptIn) {
            DevModeState.PseudoCoopDeferMpCheatPublish = false;
            MainFile.Logger.Info("[PseudoCoop] Map finish complete (no MpCheat opt-in).");
            return;
        }

        DevModeState.PseudoCoopDeferMpCheatPublish = false;
        if (!MpCheatSession.SessionArmed) {
            MainFile.Logger.Warn("[PseudoCoop] Map finish: MpCheat publish skipped (session not armed).");
            return;
        }

        MpCheatSync.TryPublishInitialHostConfig("pseudo_coop_map");
        MainFile.Logger.Info("[PseudoCoop] Map finish complete (MpCheat config published).");
    }
}
