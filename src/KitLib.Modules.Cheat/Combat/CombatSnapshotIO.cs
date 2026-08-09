using System;
using System.IO;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Combat;

internal static class CombatSnapshotIO {
    private const int FileVersion = 1;
    private static readonly byte[] Magic = [(byte)'D', (byte)'M', (byte)'C', (byte)'P'];

    internal static bool TryCapture(string path, out CombatSnapshot snapshot) {
        snapshot = default;
        var rm = RunManager.Instance;
        var combatState = CombatManager.Instance?.DebugOnlyGetState();
        var runState = rm?.DebugOnlyGetState();
        if (runState == null || combatState == null || CombatManager.Instance is not { IsInProgress: true })
            return false;

        snapshot = new CombatSnapshot {
            Round = combatState.RoundNumber,
            CurrentSide = combatState.CurrentSide,
            State = NetFullCombatState.FromRun(runState, null)
        };

        try {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            Write(path, snapshot);
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"CombatSnapshotIO: save failed: {ex.Message}");
            return false;
        }
    }

    internal static bool TryLoad(string path, out CombatSnapshot snapshot) {
        snapshot = default;
        if (!File.Exists(path))
            return false;

        try {
            snapshot = Read(path);
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"CombatSnapshotIO: load failed: {ex.Message}");
            return false;
        }
    }

    private static void Write(string path, CombatSnapshot snapshot) {
        var writer = new PacketWriter();
        writer.Reset();
        snapshot.State.Serialize(writer);
        writer.ZeroByteRemainder();
        var payloadLength = writer.BytePosition;
        var payload = new byte[payloadLength];
        Array.Copy(writer.Buffer, payload, payloadLength);

        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
        using (var bw = new BinaryWriter(fs)) {
            bw.Write(Magic);
            bw.Write(FileVersion);
            bw.Write(snapshot.Round);
            bw.Write((int)snapshot.CurrentSide);
            bw.Write(payloadLength);
            bw.Write(payload);
        }

        File.Move(tmp, path, overwrite: true);
    }

    private static CombatSnapshot Read(string path) {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);

        var magic = br.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(Magic.AsSpan()))
            throw new InvalidDataException("Invalid combat snapshot magic.");

        var version = br.ReadInt32();
        if (version != FileVersion)
            throw new InvalidDataException($"Unsupported combat snapshot version {version}.");

        var round = br.ReadInt32();
        var side = (CombatSide)br.ReadInt32();
        var payloadLength = br.ReadInt32();
        if (payloadLength <= 0)
            throw new InvalidDataException("Empty combat snapshot payload.");

        var payload = br.ReadBytes(payloadLength);
        var reader = new PacketReader();
        reader.Reset(payload);
        var state = new NetFullCombatState();
        state.Deserialize(reader);

        return new CombatSnapshot {
            Round = round,
            CurrentSide = side,
            State = state
        };
    }
}
