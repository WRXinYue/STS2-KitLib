using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace DevMode.Multiplayer.Cheat;

/// <summary>Payload bodies inside <see cref="ZzzMpCheatEnvelopeNetMessage" /> (after magic + channel).</summary>
internal static class MpCheatEnvelopeCodec {
    internal static void WriteConfig(PacketWriter writer, ulong revision, string configJson) {
        writer.WriteULong(revision);
        MpCheatPacketIO.WriteBoundedString(writer, configJson);
    }

    internal static (ulong Revision, string ConfigJson) ReadConfig(PacketReader reader) {
        var revision = reader.ReadULong();
        var json = MpCheatPacketIO.ReadBoundedString(reader);
        return (revision, json);
    }

    internal static void WriteCommand(PacketWriter writer, MpCheatCommandMessage msg) {
        writer.WriteByte((byte)msg.Kind);
        writer.WriteULong(msg.IssuedByNetId);
        writer.WriteULong(msg.CommandId);
        var add = msg.AddCard;
        writer.WriteBool(add != null);
        if (add != null)
            WriteAddCardPayload(writer, add);
        var remove = msg.RemoveCard;
        writer.WriteBool(remove != null);
        if (remove != null)
            WriteRemoveCardPayload(writer, remove);
    }

    internal static MpCheatCommandMessage ReadCommand(PacketReader reader) {
        var msg = new MpCheatCommandMessage {
            Kind = (MpCheatCommandKind)reader.ReadByte(),
            IssuedByNetId = reader.ReadULong(),
            CommandId = reader.ReadULong(),
        };
        if (reader.ReadBool())
            msg.AddCard = ReadAddCardPayload(reader);
        if (reader.ReadBool())
            msg.RemoveCard = ReadRemoveCardPayload(reader);
        return msg;
    }

    internal static void WriteAck(PacketWriter writer, MpCheatAddCardAckMessage ack) {
        writer.WriteULong(ack.CommandId);
        writer.WriteULong(ack.PeerNetId);
        writer.WriteBool(ack.Success);
        MpCheatPacketIO.WriteBoundedString(writer, ack.Error);
    }

    internal static MpCheatAddCardAckMessage ReadAck(PacketReader reader) =>
        new() {
            CommandId = reader.ReadULong(),
            PeerNetId = reader.ReadULong(),
            Success = reader.ReadBool(),
            Error = MpCheatPacketIO.ReadBoundedString(reader),
        };

    internal static void WriteAddCardPayload(PacketWriter writer, MpCheatAddCardPayload add) {
        MpCheatPacketIO.WriteBoundedString(writer, add.CardId);
        writer.WriteULong(add.TargetPlayerNetId);
        writer.WriteInt(add.Target);
        writer.WriteInt(add.Duration);
        writer.WriteInt(add.UpgradeLevels);
        writer.WriteBool(add.CustomBaseCost.HasValue);
        writer.WriteInt(add.CustomBaseCost ?? 0);
        writer.WriteBool(add.UseUpgradePreviewStyle);
    }

    internal static MpCheatAddCardPayload ReadAddCardPayload(PacketReader reader) {
        var add = new MpCheatAddCardPayload {
            CardId = MpCheatPacketIO.ReadBoundedString(reader),
            TargetPlayerNetId = reader.ReadULong(),
            Target = reader.ReadInt(),
            Duration = reader.ReadInt(),
            UpgradeLevels = reader.ReadInt(),
        };
        var hasCost = reader.ReadBool();
        add.CustomBaseCost = hasCost ? reader.ReadInt() : null;
        add.UseUpgradePreviewStyle = reader.ReadBool();
        return add;
    }

    internal static void WriteAddCardRequest(PacketWriter writer, MpCheatAddCardClientRequestMessage msg) {
        writer.WriteULong(msg.ClientRequestId);
        writer.WriteULong(msg.RequesterNetId);
        WriteAddCardPayload(writer, msg.Payload);
    }

    internal static MpCheatAddCardClientRequestMessage ReadAddCardRequest(PacketReader reader) =>
        new() {
            ClientRequestId = reader.ReadULong(),
            RequesterNetId = reader.ReadULong(),
            Payload = ReadAddCardPayload(reader),
        };

    internal static void WriteAddCardRequestResult(PacketWriter writer, MpCheatAddCardClientResultMessage msg) {
        writer.WriteULong(msg.ClientRequestId);
        writer.WriteBool(msg.Success);
        MpCheatPacketIO.WriteBoundedString(writer, msg.Message);
    }

    internal static MpCheatAddCardClientResultMessage ReadAddCardRequestResult(PacketReader reader) =>
        new() {
            ClientRequestId = reader.ReadULong(),
            Success = reader.ReadBool(),
            Message = MpCheatPacketIO.ReadBoundedString(reader),
        };

    internal static void WriteRemoveCardPayload(PacketWriter writer, MpCheatRemoveCardPayload payload) {
        MpCheatPacketIO.WriteBoundedString(writer, payload.CardId);
        writer.WriteULong(payload.TargetPlayerNetId);
        writer.WriteInt(payload.Target);
        writer.WriteInt(payload.PileIndex);
        writer.WriteBool(payload.RemoveFromRunState);
    }

    internal static MpCheatRemoveCardPayload ReadRemoveCardPayload(PacketReader reader) =>
        new() {
            CardId = MpCheatPacketIO.ReadBoundedString(reader),
            TargetPlayerNetId = reader.ReadULong(),
            Target = reader.ReadInt(),
            PileIndex = reader.ReadInt(),
            RemoveFromRunState = reader.ReadBool(),
        };

    internal static void WriteRemoveCardRequest(PacketWriter writer, MpCheatRemoveCardClientRequestMessage msg) {
        writer.WriteULong(msg.ClientRequestId);
        writer.WriteULong(msg.RequesterNetId);
        WriteRemoveCardPayload(writer, msg.Payload);
    }

    internal static MpCheatRemoveCardClientRequestMessage ReadRemoveCardRequest(PacketReader reader) =>
        new() {
            ClientRequestId = reader.ReadULong(),
            RequesterNetId = reader.ReadULong(),
            Payload = ReadRemoveCardPayload(reader),
        };
}
