namespace DevMode.Multiplayer.Cheat;

/// <summary>Locate a pile card instance + partial <see cref="DevMode.Presets.CardEditTemplate"/> JSON.</summary>
public sealed class MpCheatEditCardPayload {
    public string CardId { get; set; } = "";
    public ulong TargetPlayerNetId { get; set; }
    public int Target { get; set; }
    public int PileIndex { get; set; }
    public string TemplateJson { get; set; } = "";
}
