namespace KitLib.Multiplayer.Cheat;

/// <summary>Discriminator inside <see cref="ZzzMpCheatEnvelopeNetMessage" />.</summary>
public enum MpCheatWireChannel : byte {
    Config = 0,
    Command = 1,
    AddCardAck = 2,
    /// <summary>Client → host: request host-authoritative add-card sync.</summary>
    AddCardRequest = 3,
    /// <summary>Host → client: outcome of <see cref="AddCardRequest" />.</summary>
    AddCardRequestResult = 4,
    /// <summary>Client → host: request host-authoritative remove-card sync.</summary>
    RemoveCardRequest = 5,
    /// <summary>Host → client: outcome of <see cref="RemoveCardRequest" />.</summary>
    RemoveCardRequestResult = 6,
    /// <summary>Client → host: request host-authoritative edit-card sync.</summary>
    EditCardRequest = 7,
    /// <summary>Host → client: outcome of <see cref="EditCardRequest" />.</summary>
    EditCardRequestResult = 8,
    /// <summary>Client → host: relic/potion/combat item sync request.</summary>
    ItemRequest = 9,
    /// <summary>Host → client: outcome of <see cref="ItemRequest" />.</summary>
    ItemRequestResult = 10,
    /// <summary>Client → host: request host publish cheat config snapshot.</summary>
    ConfigRequest = 11,
    /// <summary>Host → client: outcome of <see cref="ConfigRequest" />.</summary>
    ConfigRequestResult = 12,
}
