namespace KitLib.Abstractions.Host;

public enum KitLibCardPile {
    Deck,
    Hand,
    Draw,
    Discard,
    Exhaust,
}

public enum KitLibCardDuration {
    Permanent,
    Temporary,
}

public sealed record KitLibRunItemResult(bool Ok, string? Error = null, string? ItemId = null) {
    public static KitLibRunItemResult Success(string? itemId = null) => new(true, null, itemId);
    public static KitLibRunItemResult Fail(string error) => new(false, error);
}

public sealed record KitLibAddCardRequest(
    string CardId,
    KitLibCardPile Pile = KitLibCardPile.Hand,
    KitLibCardDuration Duration = KitLibCardDuration.Permanent,
    int UpgradeLevels = 0,
    ulong? TargetPlayerNetId = null);

public sealed record KitLibRemoveCardRequest(
    KitLibCardPile Pile,
    string? CardId = null,
    int? PileIndex = null,
    bool RemoveFromRun = true,
    ulong? TargetPlayerNetId = null);

public sealed record KitLibRelicRequest(string RelicId, ulong? TargetPlayerNetId = null);

public sealed record KitLibPotionAddRequest(string PotionId, ulong? TargetPlayerNetId = null);

public sealed record KitLibPotionDiscardRequest(int SlotIndex, ulong? TargetPlayerNetId = null);

/// <summary>
/// Run inventory mutations (cards / relics / potions) wired by KitLib.Cheat.
/// String ids only — no STS2 model types on this surface.
/// </summary>
public static class KitLibRunInventoryApi {
    public static Func<bool>? IsAvailable { get; set; }

    public static Func<KitLibAddCardRequest, Task<KitLibRunItemResult>>? TryAddCard { get; set; }
    public static Func<KitLibRemoveCardRequest, Task<KitLibRunItemResult>>? TryRemoveCard { get; set; }

    public static Func<KitLibRelicRequest, Task<KitLibRunItemResult>>? TryAddRelic { get; set; }
    public static Func<KitLibRelicRequest, Task<KitLibRunItemResult>>? TryRemoveRelic { get; set; }

    public static Func<KitLibPotionAddRequest, Task<KitLibRunItemResult>>? TryAddPotion { get; set; }
    public static Func<KitLibPotionDiscardRequest, Task<KitLibRunItemResult>>? TryDiscardPotionAtSlot { get; set; }
}
