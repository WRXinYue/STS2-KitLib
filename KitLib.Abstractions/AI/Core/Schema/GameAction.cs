namespace KitLib.AI.Core.Schema;

/// <summary>A game-agnostic action the AI wants to perform.</summary>
public sealed record GameAction {
    public ActionType Type { get; init; }
    public int TargetIndex { get; init; } = -1;
    public int SecondaryIndex { get; init; } = -1;
    public string? Reason { get; init; }
}

public enum ActionType {
    Wait,
    PlayCard,
    EndTurn,
    SelectMapNode,
    PickCardReward,
    SkipCardReward,
    SelectEventChoice,
    PurchaseShopItem,
    RemoveCardAtShop,
    LeaveShop,
    Rest,
    UpgradeCard,
    UsePotion,
    DiscardPotion,
    CollectReward,
    DismissRewards,
    Proceed,
    PickRelic,
    AdvanceOverlay,
    PressConfirm,
    HandleTreasureRoom,
}
