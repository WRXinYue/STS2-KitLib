using System;
using System.Threading.Tasks;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace KitLib.AI.Sts2.Helpers;

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
            return KitLibCheatApi.IsSkipAnimSkipping?.Invoke() == true || Sts2WaitHelper.ArePlayerDrivenActionsSettled();

        var currentId = current.Id.Entry ?? "";
        if (!string.Equals(currentId, potionId, StringComparison.OrdinalIgnoreCase))
            return KitLibCheatApi.IsSkipAnimSkipping?.Invoke() == true || Sts2WaitHelper.ArePlayerDrivenActionsSettled();

        if (KitLibCheatApi.IsSkipAnimSkipping?.Invoke() != true && !Sts2WaitHelper.ArePlayerDrivenActionsSettled())
            return false;

        return false;
    }
}
