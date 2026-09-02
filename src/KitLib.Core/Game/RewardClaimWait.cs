using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace KitLib.Game;

internal static class RewardClaimWait {
    public static Task<bool> WaitForClaimAsync(
        NRewardsScreen screen,
        NRewardButton button,
        TimeSpan timeout) =>
        GameWait.Until(() => IsClaimFinished(screen, button), timeout);

    static bool IsClaimFinished(NRewardsScreen screen, NRewardButton button) {
        var top = NOverlayStack.Instance?.Peek();
        if (top is IOverlayScreen overlay && overlay != screen)
            return true;

        if (!GodotObject.IsInstanceValid(button))
            return true;

        if (!button.IsInsideTree())
            return true;

        return !button.IsEnabled;
    }
}
