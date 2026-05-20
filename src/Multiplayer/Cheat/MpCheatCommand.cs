namespace DevMode.Multiplayer.Cheat;

/// <summary>Discrete host-authoritative cheat actions (Tier 2).</summary>
public enum MpCheatCommandKind {
    KillAllEnemies = 1,
    /// <summary>Validate add-card on all peers; clients ACK before execute.</summary>
    AddCardPrepare = 2,
    /// <summary>Apply add-card after all prepare ACKs succeeded.</summary>
    AddCardExecute = 3,
    /// <summary>Validate remove-card on all peers; clients ACK before execute.</summary>
    RemoveCardPrepare = 5,
    /// <summary>Apply remove-card after all prepare ACKs succeeded.</summary>
    RemoveCardExecute = 6,
    /// <summary>Validate edit-card on all peers; clients ACK before execute.</summary>
    EditCardPrepare = 7,
    /// <summary>Apply edit-card after all prepare ACKs succeeded.</summary>
    EditCardExecute = 8,
}

public sealed class MpCheatCommandMessage {
    public MpCheatCommandKind Kind { get; set; }
    public ulong IssuedByNetId { get; set; }
    public ulong CommandId { get; set; }
    public MpCheatAddCardPayload? AddCard { get; set; }
    public MpCheatRemoveCardPayload? RemoveCard { get; set; }
    public MpCheatEditCardPayload? EditCard { get; set; }
}
