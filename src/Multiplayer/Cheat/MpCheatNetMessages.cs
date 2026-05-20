using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace DevMode.Multiplayer.Cheat;

/// <summary>Host → all: cheat config snapshot (JSON payload).</summary>
public struct MpCheatConfigNetMessage : INetMessage {
    public ulong Revision;
    public string ConfigJson;

    public readonly bool ShouldBroadcast => true;
    public readonly NetTransferMode Mode => NetTransferMode.Reliable;
    public readonly LogLevel LogLevel => LogLevel.Info;

    public void Serialize(PacketWriter writer) {
        writer.WriteULong(Revision);
        writer.WriteString(ConfigJson ?? "");
    }

    public void Deserialize(PacketReader reader) {
        Revision = reader.ReadULong();
        ConfigJson = reader.ReadString();
    }
}

/// <summary>Host → all: discrete cheat command (kill all, add-card prepare/execute).</summary>
public struct MpCheatCommandNetMessage : INetMessage {
    public MpCheatCommandKind Kind;
    public ulong IssuedByNetId;
    public ulong CommandId;
    public bool HasAddCard;
    public string CardId;
    public ulong TargetPlayerNetId;
    public int Target;
    public int Duration;
    public int UpgradeLevels;
    public bool HasCustomBaseCost;
    public int CustomBaseCost;
    public bool UseUpgradePreviewStyle;

    public readonly bool ShouldBroadcast => true;
    public readonly NetTransferMode Mode => NetTransferMode.Reliable;
    public readonly LogLevel LogLevel => LogLevel.Info;

    public void Serialize(PacketWriter writer) {
        writer.WriteByte((byte)Kind);
        writer.WriteULong(IssuedByNetId);
        writer.WriteULong(CommandId);
        writer.WriteBool(HasAddCard);
        if (!HasAddCard) return;
        writer.WriteString(CardId ?? "");
        writer.WriteULong(TargetPlayerNetId);
        writer.WriteInt(Target);
        writer.WriteInt(Duration);
        writer.WriteInt(UpgradeLevels);
        writer.WriteBool(HasCustomBaseCost);
        writer.WriteInt(CustomBaseCost);
        writer.WriteBool(UseUpgradePreviewStyle);
    }

    public void Deserialize(PacketReader reader) {
        Kind = (MpCheatCommandKind)reader.ReadByte();
        IssuedByNetId = reader.ReadULong();
        CommandId = reader.ReadULong();
        HasAddCard = reader.ReadBool();
        if (!HasAddCard) return;
        CardId = reader.ReadString();
        TargetPlayerNetId = reader.ReadULong();
        Target = reader.ReadInt();
        Duration = reader.ReadInt();
        UpgradeLevels = reader.ReadInt();
        HasCustomBaseCost = reader.ReadBool();
        CustomBaseCost = reader.ReadInt();
        UseUpgradePreviewStyle = reader.ReadBool();
    }

    public static MpCheatCommandNetMessage FromDto(MpCheatCommandMessage msg) {
        var net = new MpCheatCommandNetMessage {
            Kind = msg.Kind,
            IssuedByNetId = msg.IssuedByNetId,
            CommandId = msg.CommandId,
        };
        if (msg.AddCard == null) return net;
        net.HasAddCard = true;
        net.CardId = msg.AddCard.CardId;
        net.TargetPlayerNetId = msg.AddCard.TargetPlayerNetId;
        net.Target = msg.AddCard.Target;
        net.Duration = msg.AddCard.Duration;
        net.UpgradeLevels = msg.AddCard.UpgradeLevels;
        net.HasCustomBaseCost = msg.AddCard.CustomBaseCost.HasValue;
        net.CustomBaseCost = msg.AddCard.CustomBaseCost ?? 0;
        net.UseUpgradePreviewStyle = msg.AddCard.UseUpgradePreviewStyle;
        return net;
    }

    public MpCheatCommandMessage ToDto() {
        var dto = new MpCheatCommandMessage {
            Kind = Kind,
            IssuedByNetId = IssuedByNetId,
            CommandId = CommandId,
        };
        if (!HasAddCard) return dto;
        dto.AddCard = new MpCheatAddCardPayload {
            CardId = CardId,
            TargetPlayerNetId = TargetPlayerNetId,
            Target = Target,
            Duration = Duration,
            UpgradeLevels = UpgradeLevels,
            CustomBaseCost = HasCustomBaseCost ? CustomBaseCost : null,
            UseUpgradePreviewStyle = UseUpgradePreviewStyle,
        };
        return dto;
    }
}

/// <summary>Client → host: add-card prepare validation result.</summary>
public struct MpCheatAddCardAckNetMessage : INetMessage {
    public ulong CommandId;
    public ulong PeerNetId;
    public bool Success;
    public string Error;

    public readonly bool ShouldBroadcast => false;
    public readonly NetTransferMode Mode => NetTransferMode.Reliable;
    public readonly LogLevel LogLevel => LogLevel.Info;

    public void Serialize(PacketWriter writer) {
        writer.WriteULong(CommandId);
        writer.WriteULong(PeerNetId);
        writer.WriteBool(Success);
        writer.WriteString(Error ?? "");
    }

    public void Deserialize(PacketReader reader) {
        CommandId = reader.ReadULong();
        PeerNetId = reader.ReadULong();
        Success = reader.ReadBool();
        Error = reader.ReadString();
    }
}

internal static class MpCheatNetJson {
    internal static string SerializeConfig(MpCheatConfig config) =>
        JsonSerializer.Serialize(config);

    internal static MpCheatConfig? DeserializeConfig(string json) {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<MpCheatConfig>(json);
    }
}
