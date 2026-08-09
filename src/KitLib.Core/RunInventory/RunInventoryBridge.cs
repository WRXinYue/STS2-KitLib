using KitLib.Abstractions.Host;
using KitLib.Host;

namespace KitLib.RunInventory;

/// <summary>Public API for content mods / MCP; execution delegates to KitLib.Cheat when loaded.</summary>
public static class RunInventoryBridge {
    public static bool IsAvailable =>
        KitLibHost.IsModuleLoaded(ModuleIds.Cheat)
        && KitLibRunInventoryApi.IsAvailable?.Invoke() == true;

    public static Task<KitLibRunItemResult> TryAddCard(KitLibAddCardRequest request) {
        if (!IsAvailable || KitLibRunInventoryApi.TryAddCard == null)
            return Task.FromResult(KitLibRunItemResult.Fail("KitLib.Cheat run inventory is unavailable."));
        return KitLibRunInventoryApi.TryAddCard(request);
    }

    public static Task<KitLibRunItemResult> TryRemoveCard(KitLibRemoveCardRequest request) {
        if (!IsAvailable || KitLibRunInventoryApi.TryRemoveCard == null)
            return Task.FromResult(KitLibRunItemResult.Fail("KitLib.Cheat run inventory is unavailable."));
        return KitLibRunInventoryApi.TryRemoveCard(request);
    }

    public static Task<KitLibRunItemResult> TryAddRelic(string relicId, ulong? targetPlayerNetId = null) =>
        TryAddRelic(new KitLibRelicRequest(relicId, targetPlayerNetId));

    public static Task<KitLibRunItemResult> TryAddRelic(KitLibRelicRequest request) {
        if (!IsAvailable || KitLibRunInventoryApi.TryAddRelic == null)
            return Task.FromResult(KitLibRunItemResult.Fail("KitLib.Cheat run inventory is unavailable."));
        return KitLibRunInventoryApi.TryAddRelic(request);
    }

    public static Task<KitLibRunItemResult> TryRemoveRelic(string relicId, ulong? targetPlayerNetId = null) =>
        TryRemoveRelic(new KitLibRelicRequest(relicId, targetPlayerNetId));

    public static Task<KitLibRunItemResult> TryRemoveRelic(KitLibRelicRequest request) {
        if (!IsAvailable || KitLibRunInventoryApi.TryRemoveRelic == null)
            return Task.FromResult(KitLibRunItemResult.Fail("KitLib.Cheat run inventory is unavailable."));
        return KitLibRunInventoryApi.TryRemoveRelic(request);
    }

    public static Task<KitLibRunItemResult> TryAddPotion(string potionId, ulong? targetPlayerNetId = null) =>
        TryAddPotion(new KitLibPotionAddRequest(potionId, targetPlayerNetId));

    public static Task<KitLibRunItemResult> TryAddPotion(KitLibPotionAddRequest request) {
        if (!IsAvailable || KitLibRunInventoryApi.TryAddPotion == null)
            return Task.FromResult(KitLibRunItemResult.Fail("KitLib.Cheat run inventory is unavailable."));
        return KitLibRunInventoryApi.TryAddPotion(request);
    }

    public static Task<KitLibRunItemResult> TryDiscardPotionAtSlot(int slotIndex, ulong? targetPlayerNetId = null) =>
        TryDiscardPotionAtSlot(new KitLibPotionDiscardRequest(slotIndex, targetPlayerNetId));

    public static Task<KitLibRunItemResult> TryDiscardPotionAtSlot(KitLibPotionDiscardRequest request) {
        if (!IsAvailable || KitLibRunInventoryApi.TryDiscardPotionAtSlot == null)
            return Task.FromResult(KitLibRunItemResult.Fail("KitLib.Cheat run inventory is unavailable."));
        return KitLibRunInventoryApi.TryDiscardPotionAtSlot(request);
    }
}
