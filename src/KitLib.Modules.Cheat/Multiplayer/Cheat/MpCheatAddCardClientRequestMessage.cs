namespace KitLib.Multiplayer.Cheat;

/// <summary>Client → host: request a synced add-card (host runs prepare/ACK/execute).</summary>
public sealed class MpCheatAddCardClientRequestMessage {
    public ulong ClientRequestId { get; set; }
    public ulong RequesterNetId { get; set; }
    public MpCheatAddCardPayload Payload { get; set; } = new();
}
