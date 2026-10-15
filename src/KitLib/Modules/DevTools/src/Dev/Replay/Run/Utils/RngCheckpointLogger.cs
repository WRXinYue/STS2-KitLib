using System;
using System.IO;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Replay.Utils;

/// <summary>Optional RNG checkpoint log. Disabled unless <see cref="Enabled"/> is flipped.</summary>
internal static class RngCheckpointLogger {
    private static readonly bool Enabled = false;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Godot", "app_userdata", "Slay the Spire 2", "KitLib", "run-replays", "rng_checkpoints.log");

    internal static void Clear() {
        if (!Enabled) return;
        try { File.WriteAllText(LogPath, ""); }
        catch { /* ignore */ }
    }

    internal static void Log(string checkpoint) {
        if (!Enabled) return;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {checkpoint}\n");
        }
        catch { /* ignore */ }
    }
}
