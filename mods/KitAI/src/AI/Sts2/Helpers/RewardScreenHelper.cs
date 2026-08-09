using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace KitLib.AI.Sts2.Helpers;

/// <summary>State waits for <see cref="NRewardsScreen"/> — aligned with official handler flow.</summary>
internal static class RewardScreenHelper {
    public static Task<bool> WaitForClaimAsync(
        NRewardsScreen screen,
        NRewardButton button,
        TimeSpan timeout) =>
        Sts2WaitHelper.Until(() => IsClaimFinished(screen, button), timeout);

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
