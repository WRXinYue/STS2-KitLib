namespace DevMode.Multiplayer.Cheat;

public sealed class MpCheatItemPayload {
    public MpCheatItemKind Kind { get; set; }
    public ulong TargetPlayerNetId { get; set; }
    public string ItemId { get; set; } = "";
    public int SlotIndex { get; set; }
}
