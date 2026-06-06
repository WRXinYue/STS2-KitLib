using System;
using System.Threading.Tasks;
using DevMode;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace DevMode.AI.Sts2.Helpers;

/// <summary>Waits for <see cref="MegaCrit.Sts2.Core.Models.PotionModel.EnqueueManualUse"/> to finish.</summary>
internal static class Sts2PotionUseHelper {
    public static async Task<bool> WaitForManualUseAsync(
        Player player,
        int potionSlot,
        string potionId,
        TimeSpan timeout) {
        return await Sts2WaitHelper.Until(
            () => IsUseStable(player, potionSlot, potionId),
            timeout);
    }

    static bool IsUseStable(Player player, int potionSlot, string potionId) {
        if (NOverlayStack.Instance?.Peek() != null)
            return false;

        if (!CombatManager.Instance.IsInProgress)
            return true;

        var current = player.GetPotionAtSlotIndex(potionSlot);
        if (current == null)
            return SkipAnimControl.IsSkipping || Sts2WaitHelper.ArePlayerDrivenActionsSettled();

        var currentId = current.Id.Entry ?? "";
        if (!string.Equals(currentId, potionId, StringComparison.OrdinalIgnoreCase))
            return SkipAnimControl.IsSkipping || Sts2WaitHelper.ArePlayerDrivenActionsSettled();

        if (!SkipAnimControl.IsSkipping && !Sts2WaitHelper.ArePlayerDrivenActionsSettled())
            return false;

        return false;
    }
}
