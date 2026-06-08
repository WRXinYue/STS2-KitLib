namespace KitLib.Multiplayer.Cheat;

/// <summary>Serializable remove-card parameters broadcast to all peers.</summary>
public sealed class MpCheatRemoveCardPayload {
    public string CardId { get; set; } = "";
    public ulong TargetPlayerNetId { get; set; }
    /// <summary><see cref="CardTarget" /> pile.</summary>
    public int Target { get; set; }
    /// <summary>Index in the target pile list at prepare time.</summary>
    public int PileIndex { get; set; }
    /// <summary>Also remove from run deck state (deck tab or permanent combat removal).</summary>
    public bool RemoveFromRunState { get; set; }
}
