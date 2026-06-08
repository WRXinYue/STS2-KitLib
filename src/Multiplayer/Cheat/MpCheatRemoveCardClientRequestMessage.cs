namespace KitLib.Multiplayer.Cheat;

/// <summary>Client → host: request synced remove-card.</summary>
public sealed class MpCheatRemoveCardClientRequestMessage {
    public ulong ClientRequestId { get; set; }
    public ulong RequesterNetId { get; set; }
    public MpCheatRemoveCardPayload Payload { get; set; } = new();
}
