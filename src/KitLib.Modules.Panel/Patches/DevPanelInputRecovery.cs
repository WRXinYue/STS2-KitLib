using Godot;
using KitLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace KitLib.Patches;

/// <summary>
/// Restores click input after vanilla async flows fail mid room/screen transition.
/// Failed <see cref="NTransition.RoomFadeOut"/> leaves a fullscreen Stop layer that blocks Dev rail clicks.
/// </summary>
internal static class DevPanelInputRecovery {
    internal static void Recover() {
        if (!KitLibState.IsActive)
            return;

        var map = NRun.Instance?.GlobalUi?.MapScreen;
        if (map != null) {
            map.IsTraveling = false;
            map.SetTravelEnabled(true);
        }

        RunManager.Instance?.ActionExecutor?.Unpause();

        var transition = NGame.Instance?.Transition;
        if (transition == null || TestMode.IsOn)
            return;

        if (transition.InTransition || transition.MouseFilter == Control.MouseFilterEnum.Stop) {
            transition.MouseFilter = Control.MouseFilterEnum.Ignore;
            TaskHelper.RunSafely(transition.RoomFadeIn(showTransition: false));
        }
    }
}
