using Godot;
using KitLib;
using KitLib.Host;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Nodes;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Finishes pseudo-coop launch: MpCheat arm after EnterAct; DevPanel/publish after map opens.</summary>
internal static class PseudoCoopDeferredInit {
    public static void TryScheduleAfterEnterAct0() {
        if (!KitLibState.PseudoCoopLaunchPending) return;

        KitLibState.PseudoCoopLaunchPending = false;
        KitLog.Info("PseudoCoop", $"Scheduling deferred init (next frame)…");
        Callable.From(CompleteDeferred).CallDeferred();
    }

    static void CompleteDeferred() {
        KitLibPseudoCoopOps.EnsureGlobalUiProcessNode?.Invoke(NRun.Instance?.GlobalUi);
        KitLog.Info("PseudoCoop", $"Deferred init complete (MpCheat arm + DevPanel on map open).");
    }

    internal static void TryScheduleMapFinish() {
        if (!KitLibState.PseudoCoopAwaitingMapFinish) return;

        var run = NRun.Instance;
        if (run == null) {
            RunLateMpCheatArm();
            RunLateMpCheatPublish();
            RunLateDevPanel();
            KitLibState.PseudoCoopAwaitingMapFinish = false;
            return;
        }

        if (run.GetNodeOrNull<PseudoCoopMapFinishNode>("PseudoCoopMapFinish") != null)
            return;

        run.AddChild(new PseudoCoopMapFinishNode { Name = "PseudoCoopMapFinish" });
        KitLog.Info("PseudoCoop", $"Map finish scheduled (DevPanel + publish after map open).");
    }

    internal static void RunLateDevPanel() {
        KitLibState.PseudoCoopDeferHeavyUi = false;
        if (KitLibHost.IsDualInstanceActive?.Invoke() == true) {
            KitLog.Info("PseudoCoop", $"Map finish: minimal DevPanel (AI Host)…");
            KitLibPseudoCoopOps.AttachDualInstanceMinimalDevPanel?.Invoke();
        }
        else {
            KitLog.Info("PseudoCoop", $"Map finish: DevPanel…");
            KitLibPseudoCoopOps.AttachDeferredDevPanel?.Invoke();
        }

        if (KitLibPseudoCoopOps.IsDevPanelRailAttached?.Invoke() == true)
            KitLog.Info("PseudoCoop", $"DevPanel attached.");
        else
            KitLog.Warn("PseudoCoop", $"DevPanel attach skipped (DevMode inactive or UI unavailable).");

        if (KitLibHost.IsDualInstanceActive?.Invoke() == true)
            KitLibPseudoCoopOps.RunDualInstanceLanPresets?.Invoke();
    }

    internal static void RunLateMpCheatArm() {
        if (!MpCheatSession.LocalOptIn) {
            KitLog.Info("PseudoCoop", $"MpCheat arm skipped (no opt-in).");
            return;
        }

        MpCheatSession.TryArmSession("map_finish", allowWhileDeferredUi: true);
        if (!MpCheatSession.SessionArmed) {
            KitLog.Warn("PseudoCoop", $"MpCheat arm failed: {MpCheatSession.LastBlockReason ?? "unknown"}.");
            return;
        }

        KitLog.Info("PseudoCoop", $"MpCheat armed (publish deferred until map).");
    }

    internal static void RunLateMpCheatPublish() {
        if (!MpCheatSession.LocalOptIn) {
            KitLibState.PseudoCoopDeferMpCheatPublish = false;
            KitLog.Info("PseudoCoop", $"Map finish complete (no MpCheat opt-in).");
            return;
        }

        KitLibState.PseudoCoopDeferMpCheatPublish = false;
        if (!MpCheatSession.SessionArmed) {
            KitLog.Warn("PseudoCoop", $"Map finish: MpCheat publish skipped (session not armed).");
            return;
        }

        MpCheatSync.TryPublishInitialHostConfig("pseudo_coop_map");
        KitLog.Info("PseudoCoop", $"Map finish complete (MpCheat config published).");
    }
}
