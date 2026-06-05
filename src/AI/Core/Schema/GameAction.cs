namespace DevMode.AI.Core.Schema;

/// <summary>
/// A game-agnostic action the AI wants to perform.
/// </summary>
public sealed record GameAction
{
    public ActionType Type { get; init; }

    /// <summary>Primary target index (e.g. card index in hand, map node index).</summary>
    public int TargetIndex { get; init; } = -1;

    /// <summary>Secondary target: combat enemy index (0-based slot in <c>CombatState.Enemies</c>, matches snapshot <c>enemy["index"]</c>).</summary>
    public int SecondaryIndex { get; init; } = -1;

    /// <summary>Human-readable explanation of why this action was chosen.</summary>
    public string? Reason { get; init; }
}

public enum ActionType
{
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
    /// <summary>Choose a relic on <c>NChooseARelicSelection</c> (<see cref="GamePhase.RelicSelection"/>).</summary>
    PickRelic,
    /// <summary>Try to dismiss the top overlay (proceed, skip, or first relic choice).</summary>
    AdvanceOverlay,
    /// <summary>Simulate ui_accept — skip death animations / tap-to-continue interstitials.</summary>
    PressConfirm,
    /// <summary>Open chest, pick up relics, and proceed in treasure room.</summary>
    HandleTreasureRoom,
}
