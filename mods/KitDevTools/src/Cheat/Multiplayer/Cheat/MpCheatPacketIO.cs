using System;
using System.Text;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;

namespace KitLib.Multiplayer.Cheat;

/// <summary>Wire helpers for MpCheat INetMessage payloads (magic + bounded strings).</summary>
internal static class MpCheatPacketIO {
    /// <summary>DevMode MpCheat wire magic: "DMC0".</summary>
    internal const uint Magic = 0x444D4330;

    internal const int MaxStringBytes = 64 * 1024;

    internal static void WriteMagic(PacketWriter writer) => writer.WriteUInt(Magic);

    internal static void ReadMagic(PacketReader reader) {
        var magic = reader.ReadUInt();
        if (magic != Magic)
            throw new InvalidOperationException($"MpCheat bad magic: 0x{magic:X8}");
    }

    internal static void WriteBoundedString(PacketWriter writer, string? value) =>
        writer.WriteString(value ?? "");

    internal static string ReadBoundedString(PacketReader reader) {
        var len = reader.ReadInt();
        if (len < 0 || len > MaxStringBytes)
            throw new InvalidOperationException($"MpCheat string length {len} out of range (max {MaxStringBytes}).");
        if (len == 0) return "";
        var bytes = new byte[len];
        reader.ReadBytes(bytes, len);
        return Encoding.UTF8.GetString(bytes);
    }
}
