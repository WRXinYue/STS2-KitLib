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
        if (add == null) return;
        MpCheatPacketIO.WriteBoundedString(writer, add.CardId);
        writer.WriteULong(add.TargetPlayerNetId);
        writer.WriteInt(add.Target);
        writer.WriteInt(add.Duration);
        writer.WriteInt(add.UpgradeLevels);
        writer.WriteBool(add.CustomBaseCost.HasValue);
        writer.WriteInt(add.CustomBaseCost ?? 0);
        writer.WriteBool(add.UseUpgradePreviewStyle);
    }

    internal static MpCheatCommandMessage ReadCommand(PacketReader reader) {
        var msg = new MpCheatCommandMessage {
            Kind = (MpCheatCommandKind)reader.ReadByte(),
            IssuedByNetId = reader.ReadULong(),
            CommandId = reader.ReadULong(),
        };
        if (!reader.ReadBool()) return msg;
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
        msg.AddCard = add;
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
}
