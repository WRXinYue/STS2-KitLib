using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI.AutoPlay.Scoring;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace KitLib.AI.Sts2.Helpers;

/// <summary>Resolves potion-belt-full prompts opened by reward or shop flows.</summary>
internal static class PotionRewardHelper {
    public static async Task<bool> TryResolveFullBeltPrompt(
        Player player,
        JsonObject snapshot,
        int preferredDiscardSlot = -1) {
        var slot = preferredDiscardSlot;
        if (slot < 0) {
            var lowest = PotionInventoryScorer.FindLowestHeld(snapshot);
            if (lowest == null)
                return false;
            slot = lowest.Value.Slot;
        }

        var top = NOverlayStack.Instance?.Peek();
        if (top is not Node overlay)
            return false;

        var holders = UIHelper.FindAll<NPotionHolder>(overlay)
            .Where(h => GodotObject.IsInstanceValid(h) && h.IsInsideTree())
            .ToList();
        if (holders.Count == 0)
            return false;

        NPotionHolder? target = null;
        if (slot >= 0 && slot < holders.Count)
            target = holders[slot];
        target ??= holders.FirstOrDefault(h => h.Visible);

        if (target == null)
            return false;

        await UIHelper.Click(target);
        await Sts2WaitHelper.Until(
            () => !GodotObject.IsInstanceValid(target) || !target.IsEnabled || player.HasOpenPotionSlots,
            TimeSpan.FromSeconds(5));
        return player.HasOpenPotionSlots;
    }
}
