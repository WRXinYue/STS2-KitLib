using System;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace DevMode.Multiplayer.Cheat;

/// <summary>
/// Single INetMessage slot for all MpCheat wire traffic (config / command / ACK).
/// Type name sorts late (Zzz*) to reduce id collisions with vanilla + other mods.
/// </summary>
public struct ZzzMpCheatEnvelopeNetMessage : INetMessage {
    public MpCheatWireChannel Channel;
    public ulong ConfigRevision;
    public string ConfigJson;
    public MpCheatCommandMessage Command;
    public MpCheatAddCardAckMessage Ack;

    public readonly bool ShouldBroadcast =>
        Channel is MpCheatWireChannel.Config or MpCheatWireChannel.Command;

    public readonly NetTransferMode Mode => NetTransferMode.Reliable;

    public readonly LogLevel LogLevel =>
        Channel == MpCheatWireChannel.AddCardAck ? LogLevel.Debug : LogLevel.Info;

    public void Serialize(PacketWriter writer) {
        MpCheatPacketIO.WriteMagic(writer);
        writer.WriteByte((byte)Channel);
        switch (Channel) {
            case MpCheatWireChannel.Config:
                MpCheatEnvelopeCodec.WriteConfig(writer, ConfigRevision, ConfigJson);
                break;
            case MpCheatWireChannel.Command:
                MpCheatEnvelopeCodec.WriteCommand(writer, Command);
                break;
            case MpCheatWireChannel.AddCardAck:
                MpCheatEnvelopeCodec.WriteAck(writer, Ack);
                break;
            default:
                throw new InvalidOperationException($"MpCheat unknown channel: {Channel}");
        }
    }

    public void Deserialize(PacketReader reader) {
        MpCheatPacketIO.ReadMagic(reader);
        Channel = (MpCheatWireChannel)reader.ReadByte();
        switch (Channel) {
            case MpCheatWireChannel.Config: {
                var (revision, json) = MpCheatEnvelopeCodec.ReadConfig(reader);
                ConfigRevision = revision;
                ConfigJson = json;
                break;
            }
            case MpCheatWireChannel.Command:
                Command = MpCheatEnvelopeCodec.ReadCommand(reader);
                break;
            case MpCheatWireChannel.AddCardAck:
                Ack = MpCheatEnvelopeCodec.ReadAck(reader);
                break;
            default:
                throw new InvalidOperationException($"MpCheat unknown channel: {Channel}");
        }
    }

    public static ZzzMpCheatEnvelopeNetMessage FromConfig(ulong revision, string configJson) =>
        new() {
            Channel = MpCheatWireChannel.Config,
            ConfigRevision = revision,
            ConfigJson = configJson,
        };

    public static ZzzMpCheatEnvelopeNetMessage FromCommand(MpCheatCommandMessage command) =>
        new() {
            Channel = MpCheatWireChannel.Command,
            Command = command,
        };

    public static ZzzMpCheatEnvelopeNetMessage FromAck(MpCheatAddCardAckMessage ack) =>
        new() {
            Channel = MpCheatWireChannel.AddCardAck,
            Ack = ack,
        };
}

internal static class MpCheatNetJson {
    internal static string SerializeConfig(MpCheatConfig config) =>
        JsonSerializer.Serialize(config);

    internal static MpCheatConfig? DeserializeConfig(string json) {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<MpCheatConfig>(json);
    }
}
