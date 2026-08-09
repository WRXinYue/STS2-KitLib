namespace KitLib.Multiplayer.Cheat;

/// <summary>Client → host validation result for a pending add/remove-card command.</summary>
public sealed class MpCheatAddCardAckMessage {
    public ulong CommandId { get; set; }
    public ulong PeerNetId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
