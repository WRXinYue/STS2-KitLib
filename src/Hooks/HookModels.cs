using System.Collections.Generic;

namespace KitLib.Hooks;

public enum TriggerType {
    CombatStart,
    CombatEnd,
    TurnStart,
    TurnEnd,
    OnDraw,
    OnDamageDealt,
    OnDamageTaken,
    OnPotionUsed,
    OnCardPlayed,
    OnShuffle,
}

public enum ActionType {
    ApplyPower,
    AddCard,
    SaveSlot,
    UsePotion,
}

public enum ConditionType {
    None,
    HpBelow,
    HpAbove,
    FloorAbove,
    FloorBelow,
    HasPower,
    NotHasPower,
}

public enum HookTargetType {
    Player,
    AllEnemies,
    Allies,
}

public sealed class HookCondition {
    public ConditionType Type { get; set; } = ConditionType.None;
    /// <summary>Threshold percentage (0-100) for HP conditions, floor number, or PowerId string.</summary>
    public string Value { get; set; } = "";
}

public sealed class HookAction {
    public ActionType Type { get; set; }
    /// <summary>PowerId, CardId, or PotionId depending on <see cref="Type"/>.</summary>
    public string TargetId { get; set; } = "";
    public int Amount { get; set; } = 1;
    public HookTargetType Target { get; set; } = HookTargetType.Player;
    /// <summary>Save slot index (for <see cref="ActionType.SaveSlot"/>).</summary>
    public int SlotIndex { get; set; } = 0;
}

public sealed class HookEntry {
    public string Name { get; set; } = "";
    public TriggerType Trigger { get; set; }
    public List<HookCondition> Conditions { get; set; } = [];
    public List<HookAction> Actions { get; set; } = [];
    public bool Enabled { get; set; } = true;
}
