namespace DevMode.Multiplayer.Cheat;

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
}
