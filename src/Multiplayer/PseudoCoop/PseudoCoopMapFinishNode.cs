using Godot;
using MegaCrit.Sts2.Core.Combat;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Arms MpCheat, attaches DevPanel, publishes — only when map is open and combat is idle.</summary>
internal partial class PseudoCoopMapFinishNode : Node {
    internal const int FramesBetweenSteps = 45;

    int _countdown;
    int _step;

    public override void _Process(double delta) {
        // Neow map open can overlap first combat load; defer heavy init until safe.
        if (CombatManager.Instance is { IsInProgress: true }) return;

        if (_countdown > 0) {
            _countdown--;
            return;
        }

        switch (_step++) {
            case 0:
                PseudoCoopDeferredInit.RunLateMpCheatArm();
                _countdown = FramesBetweenSteps;
                break;
            case 1:
                PseudoCoopDeferredInit.RunLateMpCheatPublish();
                _countdown = FramesBetweenSteps;
                break;
            case 2:
                PseudoCoopDeferredInit.RunLateDevPanel();
                KitLibState.PseudoCoopAwaitingMapFinish = false;
                QueueFree();
                break;
        }
    }
}
