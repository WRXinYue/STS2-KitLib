using System;
using System.Threading.Tasks;
using KitLib.Abstractions.Host;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace KitLib.Game;

internal static class PotionUseWait {
    public static Task<bool> WaitForManualUseAsync(
        Player player,
        int potionSlot,
        string potionId,
        TimeSpan timeout) =>
        GameWait.Until(() => IsUseStable(player, potionSlot, potionId), timeout);

    static bool IsUseStable(Player player, int potionSlot, string potionId) {
        if (NOverlayStack.Instance?.Peek() != null)
            return false;

        if (!CombatManager.Instance.IsInProgress)
            return true;

        var current = player.GetPotionAtSlotIndex(potionSlot);
        if (current == null)
            return KitLibCheatApi.IsSkipAnimSkipping?.Invoke() == true || GameWait.ArePlayerDrivenActionsSettled();

        var currentId = current.Id.Entry ?? "";
        if (!string.Equals(currentId, potionId, StringComparison.OrdinalIgnoreCase))
            return KitLibCheatApi.IsSkipAnimSkipping?.Invoke() == true || GameWait.ArePlayerDrivenActionsSettled();

        if (KitLibCheatApi.IsSkipAnimSkipping?.Invoke() != true && !GameWait.ArePlayerDrivenActionsSettled())
            return false;

        return false;
    }
}
