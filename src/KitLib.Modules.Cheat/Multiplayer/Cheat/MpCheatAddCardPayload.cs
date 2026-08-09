namespace KitLib.Multiplayer.Cheat;

/// <summary>Serializable add-card parameters broadcast to all peers.</summary>
public sealed class MpCheatAddCardPayload {
    public string CardId { get; set; } = "";
    public ulong TargetPlayerNetId { get; set; }
    public int Target { get; set; }
    public int Duration { get; set; }
    public int UpgradeLevels { get; set; }
    public int? CustomBaseCost { get; set; }
    /// <summary>Partial <see cref="KitLib.Presets.CardEditTemplate"/> JSON applied after create on all peers.</summary>
    public string TemplateJson { get; set; } = "";
    public bool UseUpgradePreviewStyle { get; set; }
}
