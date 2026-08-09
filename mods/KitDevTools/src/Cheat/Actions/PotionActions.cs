using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Actions;

internal static class PotionActions {
    private const BindingFlags ReflFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static IEnumerable<PotionModel> GetAllPotions() => ModelDb.AllPotions;

    public static async Task AddPotion(Player player, PotionModel canonicalPotion) {
        if (MpCheatSession.InMultiplayerRun) {
            MainFile.Logger.Warn(
                $"PotionActions: Cannot add {((AbstractModel)canonicalPotion).Id.Entry} locally in multiplayer — use host potion sync.");
            return;
        }

        await PotionCmd.TryToProcure(canonicalPotion.ToMutable(), player);
    }

    public static async Task DiscardPotion(PotionModel ownedPotion) {
        if (MpCheatSession.InMultiplayerRun) {
            MainFile.Logger.Warn("PotionActions: Cannot discard potion locally in multiplayer — use host potion sync.");
            return;
        }

        await PotionCmd.Discard(ownedPotion);
    }

    public static string GetPotionDisplayName(PotionModel potion) {
        try { return potion.Title?.GetFormattedText() ?? ((AbstractModel)potion).Id.Entry ?? "?"; }
        catch { return ((AbstractModel)potion).Id.Entry ?? "?"; }
    }

    /// <summary>Formatted potion body text; uses reflection so it survives STS2 renames (<c>Description</c> → <c>DynamicDescription</c>, etc.).</summary>
    public static string? GetPotionDescriptionFormatted(PotionModel potion) {
        foreach (var name in new[] { "DynamicDescription", "Description", "_descriptionLocString" }) {
            try {
                var prop = typeof(PotionModel).GetProperty(name, ReflFlags);
                if (prop?.GetValue(potion) is LocString loc)
                    return loc.GetFormattedText();
                var field = typeof(PotionModel).GetField(name, ReflFlags);
                if (field?.GetValue(potion) is LocString loc2)
                    return loc2.GetFormattedText();
            }
            catch { }
        }

        return null;
    }

    internal static PotionModel? FindPotionById(string potionId) {
        if (string.IsNullOrEmpty(potionId)) return null;
        return ModelDb.AllPotions.FirstOrDefault(p => ((AbstractModel)p).Id.Entry == potionId);
    }

    internal static int GetPotionSlotIndex(Player player, PotionModel ownedPotion) {
        for (var i = 0; i < player.MaxPotionCount; i++) {
            if (player.GetPotionAtSlotIndex(i) == ownedPotion)
                return i;
        }
        return -1;
    }

    private static int CountFilledPotionSlots(Player player) {
        var count = 0;
        for (var i = 0; i < player.MaxPotionCount; i++) {
            if (player.GetPotionAtSlotIndex(i) != null)
                count++;
        }
        return count;
    }

    internal static bool TryValidateAddPotion(MpCheatItemPayload payload, out string? error) {
        error = null;
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player == null) {
            error = "target player not found";
            return false;
        }

        if (FindPotionById(payload.ItemId) == null) {
            error = "potion not found";
            return false;
        }

        if (CountFilledPotionSlots(player) >= player.MaxPotionCount) {
            error = I18N.T("mpcheat.potionAdd.full", "Potion belt is full.");
            return false;
        }

        return true;
    }

    internal static bool TryValidateRemovePotion(MpCheatItemPayload payload, out string? error) {
        error = null;
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player == null) {
            error = "target player not found";
            return false;
        }

        if (payload.SlotIndex < 0 || payload.SlotIndex >= player.MaxPotionCount
            || player.GetPotionAtSlotIndex(payload.SlotIndex) == null) {
            error = "invalid potion slot";
            return false;
        }

        return true;
    }

    internal static async Task ExecuteAddPotionFromMpSync(MpCheatItemPayload payload) {
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        var potion = FindPotionById(payload.ItemId);
        if (player == null || potion == null) return;

        await PotionCmd.TryToProcure(potion.ToMutable(), player);
        MainFile.Logger.Info($"PotionActions: MP sync added potion {payload.ItemId} to {player.NetId}");
    }

    internal static async Task ExecuteDiscardPotionFromMpSync(MpCheatItemPayload payload) {
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player == null) return;

        var owned = player.GetPotionAtSlotIndex(payload.SlotIndex);
        if (owned == null) return;

        await PotionCmd.Discard(owned);
        MainFile.Logger.Info($"PotionActions: MP sync discarded potion slot {payload.SlotIndex} from {player.NetId}");
    }
}
