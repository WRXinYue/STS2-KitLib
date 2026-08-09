namespace KitLib.Multiplayer.Cheat;

public sealed class MpCheatItemPayload {
    public MpCheatItemKind Kind { get; set; }
    public ulong TargetPlayerNetId { get; set; }
    public string ItemId { get; set; } = "";
    public int SlotIndex { get; set; }
    public int Amount { get; set; } = 1;
    public int PowerTarget { get; set; }
}
