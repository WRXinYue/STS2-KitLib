namespace KitLib.Multiplayer.Cheat;

/// <summary>Host → client: result of <see cref="MpCheatAddCardClientRequestMessage" />.</summary>
public sealed class MpCheatAddCardClientResultMessage {
    public ulong ClientRequestId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
