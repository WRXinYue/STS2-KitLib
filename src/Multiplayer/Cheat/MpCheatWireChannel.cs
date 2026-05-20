namespace DevMode.Multiplayer.Cheat;

/// <summary>Discriminator inside <see cref="ZzzMpCheatEnvelopeNetMessage" />.</summary>
public enum MpCheatWireChannel : byte {
    Config = 0,
    Command = 1,
    AddCardAck = 2,
}
