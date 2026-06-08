using System;
using System.Text.Json;
using KitLib.Presets;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace KitLib.Multiplayer.Cheat;

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
    public MpCheatAddCardClientRequestMessage AddCardRequest;
    public MpCheatAddCardClientResultMessage AddCardRequestResult;
    public MpCheatRemoveCardClientRequestMessage RemoveCardRequest;
    public MpCheatEditCardClientRequestMessage EditCardRequest;
    public MpCheatItemClientRequestMessage ItemRequest;
    public MpCheatConfigClientRequestMessage ConfigRequest;

    public readonly bool ShouldBroadcast =>
        Channel is MpCheatWireChannel.Config or MpCheatWireChannel.Command;

    public readonly NetTransferMode Mode => NetTransferMode.Reliable;

    public readonly LogLevel LogLevel =>
        Channel is MpCheatWireChannel.AddCardAck
            or MpCheatWireChannel.AddCardRequestResult
            or MpCheatWireChannel.RemoveCardRequestResult
            or MpCheatWireChannel.EditCardRequestResult
            or MpCheatWireChannel.ItemRequestResult
            or MpCheatWireChannel.ConfigRequestResult
            ? LogLevel.Debug
            : LogLevel.Info;

    public readonly bool ShouldBuffer => true;

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
            case MpCheatWireChannel.AddCardRequest:
                MpCheatEnvelopeCodec.WriteAddCardRequest(writer, AddCardRequest);
                break;
            case MpCheatWireChannel.AddCardRequestResult:
            case MpCheatWireChannel.RemoveCardRequestResult:
            case MpCheatWireChannel.EditCardRequestResult:
            case MpCheatWireChannel.ItemRequestResult:
                MpCheatEnvelopeCodec.WriteAddCardRequestResult(writer, AddCardRequestResult);
                break;
            case MpCheatWireChannel.ItemRequest:
                MpCheatEnvelopeCodec.WriteItemRequest(writer, ItemRequest);
                break;
            case MpCheatWireChannel.ConfigRequest:
                MpCheatEnvelopeCodec.WriteConfigRequest(writer, ConfigRequest);
                break;
            case MpCheatWireChannel.ConfigRequestResult:
                MpCheatEnvelopeCodec.WriteAddCardRequestResult(writer, AddCardRequestResult);
                break;
            case MpCheatWireChannel.RemoveCardRequest:
                MpCheatEnvelopeCodec.WriteRemoveCardRequest(writer, RemoveCardRequest);
                break;
            case MpCheatWireChannel.EditCardRequest:
                MpCheatEnvelopeCodec.WriteEditCardRequest(writer, EditCardRequest);
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
            case MpCheatWireChannel.AddCardRequest:
                AddCardRequest = MpCheatEnvelopeCodec.ReadAddCardRequest(reader);
                break;
            case MpCheatWireChannel.AddCardRequestResult:
            case MpCheatWireChannel.RemoveCardRequestResult:
            case MpCheatWireChannel.EditCardRequestResult:
            case MpCheatWireChannel.ItemRequestResult:
                AddCardRequestResult = MpCheatEnvelopeCodec.ReadAddCardRequestResult(reader);
                break;
            case MpCheatWireChannel.ItemRequest:
                ItemRequest = MpCheatEnvelopeCodec.ReadItemRequest(reader);
                break;
            case MpCheatWireChannel.ConfigRequest:
                ConfigRequest = MpCheatEnvelopeCodec.ReadConfigRequest(reader);
                break;
            case MpCheatWireChannel.ConfigRequestResult:
                AddCardRequestResult = MpCheatEnvelopeCodec.ReadAddCardRequestResult(reader);
                break;
            case MpCheatWireChannel.RemoveCardRequest:
                RemoveCardRequest = MpCheatEnvelopeCodec.ReadRemoveCardRequest(reader);
                break;
            case MpCheatWireChannel.EditCardRequest:
                EditCardRequest = MpCheatEnvelopeCodec.ReadEditCardRequest(reader);
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

    public static ZzzMpCheatEnvelopeNetMessage FromAddCardRequest(MpCheatAddCardClientRequestMessage request) =>
        new() {
            Channel = MpCheatWireChannel.AddCardRequest,
            AddCardRequest = request,
        };

    public static ZzzMpCheatEnvelopeNetMessage FromAddCardRequestResult(MpCheatAddCardClientResultMessage result) =>
        new() {
            Channel = MpCheatWireChannel.AddCardRequestResult,
            AddCardRequestResult = result,
        };

    public static ZzzMpCheatEnvelopeNetMessage FromRemoveCardRequest(MpCheatRemoveCardClientRequestMessage request) =>
        new() {
            Channel = MpCheatWireChannel.RemoveCardRequest,
            RemoveCardRequest = request,
        };

    public static ZzzMpCheatEnvelopeNetMessage FromRemoveCardRequestResult(MpCheatAddCardClientResultMessage result) =>
        new() {
            Channel = MpCheatWireChannel.RemoveCardRequestResult,
            AddCardRequestResult = result,
        };

    public static ZzzMpCheatEnvelopeNetMessage FromEditCardRequest(MpCheatEditCardClientRequestMessage request) =>
        new() {
            Channel = MpCheatWireChannel.EditCardRequest,
            EditCardRequest = request,
        };

    public static ZzzMpCheatEnvelopeNetMessage FromEditCardRequestResult(MpCheatAddCardClientResultMessage result) =>
        new() {
            Channel = MpCheatWireChannel.EditCardRequestResult,
            AddCardRequestResult = result,
        };

    public static ZzzMpCheatEnvelopeNetMessage FromItemRequest(MpCheatItemClientRequestMessage request) =>
        new() {
            Channel = MpCheatWireChannel.ItemRequest,
            ItemRequest = request,
        };

    public static ZzzMpCheatEnvelopeNetMessage FromItemRequestResult(MpCheatAddCardClientResultMessage result) =>
        new() {
            Channel = MpCheatWireChannel.ItemRequestResult,
            AddCardRequestResult = result,
        };

    public static ZzzMpCheatEnvelopeNetMessage FromConfigRequest(MpCheatConfigClientRequestMessage request) =>
        new() {
            Channel = MpCheatWireChannel.ConfigRequest,
            ConfigRequest = request,
        };

    public static ZzzMpCheatEnvelopeNetMessage FromConfigRequestResult(MpCheatAddCardClientResultMessage result) =>
        new() {
            Channel = MpCheatWireChannel.ConfigRequestResult,
            AddCardRequestResult = result,
        };
}

internal static class MpCheatNetJson {
    internal static string SerializeConfig(MpCheatConfig config) =>
        JsonSerializer.Serialize(config);

    internal static MpCheatConfig? DeserializeConfig(string json) {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<MpCheatConfig>(json);
    }

    internal static string SerializeEditTemplate(CardEditTemplate template) =>
        JsonSerializer.Serialize(template);

    internal static CardEditTemplate? DeserializeEditTemplate(string json) {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<CardEditTemplate>(json);
    }
}
