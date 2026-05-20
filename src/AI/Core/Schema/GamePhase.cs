namespace DevMode.AI.Core.Schema;

/// <summary>
/// Game-agnostic phases that any card/board game AI might encounter.
/// Concrete games map their specific screens/states to these phases.
/// </summary>
public enum GamePhase
{
    None,
    Combat,
    MapSelection,
    CardReward,
    EventChoice,
    Shop,
    RestSite,
    RewardScreen,
    BossReward,
    GameOver,
    Victory,
    /// <summary>Overlay <c>NChooseARelicSelection</c> — pick one of several relics.</summary>
    RelicSelection,
    /// <summary>Combat just ended; death VFX / interstitial before rewards — confirm to advance.</summary>
    PostCombatTransition,
    /// <summary>Treasure room — open chest, pick up relics, proceed.</summary>
    TreasureRoom,
    Unknown,
}
