namespace KitLib.Multiplayer.Cheat;

public sealed class MpCheatEditCardClientRequestMessage {
    public ulong ClientRequestId { get; set; }
    public ulong RequesterNetId { get; set; }
    public MpCheatEditCardPayload Payload { get; set; } = new();
}
