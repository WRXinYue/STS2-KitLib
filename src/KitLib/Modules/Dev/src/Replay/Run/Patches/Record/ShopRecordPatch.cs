using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace KitLib.Replay.Patches.Record;

using KitLib.Replay;
using KitLib.Replay.Commands;

/// <summary>
/// Shared state for shop purchase recording.
///
/// IsPurchasing: suppresses duplicate SyncLocalObtained* recordings in
///   BattleRewardPatch while a merchant purchase is in progress.
///
/// PendingLabel: the Buy* string captured in the OnTryPurchaseUnsynchronized prefix,
///   before ClearAfterPurchase() nulls out the entry's item reference.
///   Written by InvokePurchaseCompleted (on success) or cleared by
///   InvokePurchaseFailed (on failure).
/// </summary>
internal static class ShopPurchaseState {
    internal static bool IsPurchasing;
    internal static string? PendingLabel;
}

/// <summary>
/// Recording patches for merchant shop interactions.
///
/// Item details (card title etc.) are read in the OnTryPurchaseUnsynchronized PREFIX
/// because ClearAfterPurchase() nulls the entry's item reference before
/// InvokePurchaseCompleted fires.
///
/// InvokePurchaseCompleted prefix writes the captured label and clears state.
/// InvokePurchaseFailed prefix clears state so the flag never stays stuck.
///
/// Card removal is recorded at its implementation entry before the card picker opens.
/// </summary>

// ── Open shop ─────────────────────────────────────────────────────────────────

[HarmonyPatch(typeof(NMerchantRoom), nameof(NMerchantRoom.OpenInventory))]
public static class ShopOpenRecordPatch {
    [HarmonyPrefix]
    public static void Prefix(NMerchantRoom __instance) {
        // Only record when the inventory is actually about to open.
        // OpenInventory() is guarded by `if (!Inventory.IsOpen)` in the game,
        // so any call while already open is a no-op.  Without this check a
        // second call (e.g. from a UI refresh) would record a duplicate OpenShop
        // that later stalls ProcessNextPurchase during replay.
        if (__instance.Inventory?.IsOpen == false)
            PlayerActionBuffer.Record(new OpenShopCommand().ToString());
    }
}

// ── Capture item label and set flag on purchase start ─────────────────────────

#if STS2_V113_OR_GREATER
[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseUnsynchronized))]
#else
[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper))]
#endif
public static class ShopPurchaseStartPatch {
    [HarmonyPrefix]
    public static void Prefix(MerchantEntry __instance) {
#if STS2_V113_OR_GREATER
        if (!LocalContext.IsMe(__instance.Player))
            return;
#endif
        if (__instance is MerchantCardRemovalEntry)
            return;

        // Record immediately so the command appears in the log as soon as
        // the player clicks.  If the purchase fails, UndoLast removes it.
        string? label = __instance switch {
            MerchantCardEntry card => card.CreationResult?.Card?.Title is string t
                                             ? new BuyCardCommand(t).ToString() : null,
            MerchantRelicEntry relic => relic.Model != null
                                             ? new BuyRelicCommand(relic.Model.Title.GetFormattedText()).ToString() : null,
            MerchantPotionEntry potion => potion.Model != null
                                             ? new BuyPotionCommand(potion.Model.Title.GetFormattedText()).ToString() : null,
            _ => null
        };
        if (label != null)
            PlayerActionBuffer.Record(label);
        ShopPurchaseState.PendingLabel = label;
        ShopPurchaseState.IsPurchasing = true;
    }
}

#if STS2_V113_OR_GREATER
[HarmonyPatch(typeof(MerchantCardRemovalEntry), "OnTryPurchaseInternal")]
#else
[HarmonyPatch(typeof(MerchantCardRemovalEntry), nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper))]
#endif
public static class ShopCardRemovalPurchaseStartPatch {
    [HarmonyPrefix]
    public static void Prefix(MerchantCardRemovalEntry __instance) {
#if STS2_V113_OR_GREATER
        if (!LocalContext.IsMe(__instance.Player))
            return;
#endif

        PlayerActionBuffer.Record(new BuyCardRemovalCommand().ToString());
        ShopPurchaseState.PendingLabel = null;
        ShopPurchaseState.IsPurchasing = true;
    }
}

// ── Record on success and clear state ────────────────────────────────────────

[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.InvokePurchaseCompleted))]
public static class ShopPurchaseCompletedPatch {
    [HarmonyPrefix]
    public static void Prefix(MerchantEntry __instance) {
#if STS2_V113_OR_GREATER
        if (!LocalContext.IsMe(__instance.Player))
            return;
#endif

        // Already recorded at purchase start — just clear state.
        ShopPurchaseState.IsPurchasing = false;
        ShopPurchaseState.PendingLabel = null;
    }
}

// ── Clear state on failure ────────────────────────────────────────────────────

[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.InvokePurchaseFailed))]
public static class ShopPurchaseFailedPatch {
    [HarmonyPrefix]
    public static void Prefix(MerchantEntry __instance) {
#if STS2_V113_OR_GREATER
        if (!LocalContext.IsMe(__instance.Player))
            return;
#endif

        // Purchase failed — undo the speculatively recorded entry.
        if (ShopPurchaseState.PendingLabel != null)
            PlayerActionBuffer.UndoLast();
        ShopPurchaseState.IsPurchasing = false;
        ShopPurchaseState.PendingLabel = null;
    }
}
