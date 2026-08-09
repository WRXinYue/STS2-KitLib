namespace KitLib.Multiplayer.Cheat;

public sealed class MpCheatItemClientRequestMessage {
    public ulong ClientRequestId { get; set; }
    public ulong RequesterNetId { get; set; }
    public MpCheatItemPayload Payload { get; set; } = new();
}
