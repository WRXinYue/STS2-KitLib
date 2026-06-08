namespace KitLib.Multiplayer.Cheat;

public sealed class MpCheatConfigClientRequestMessage {
    public ulong ClientRequestId { get; set; }
    public ulong RequesterNetId { get; set; }
    public string ConfigJson { get; set; } = "";
}
