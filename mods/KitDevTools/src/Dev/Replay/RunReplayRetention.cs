using System;
using System.IO;
using System.Linq;
using Godot;

namespace KitLib.Replay;

/// <summary>
/// Keeps only the newest full-run replay files. Diagnostic logs in the same
/// folder are left alone.
/// </summary>
internal static class RunReplayRetention {
    internal static void Prune() {
        int keep = DevToolsSettings.RunReplayKeepCount;
        string dir = CombatReplayPlayback.RunReplayRootDirectory();
        if (!Directory.Exists(dir))
            return;

        FileInfo[] files;
        try {
            files = new DirectoryInfo(dir)
                .EnumerateFiles()
                .Where(IsRunReplayFile)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToArray();
        }
        catch (Exception ex) {
            GD.PrintErr($"[KitLib.RunReplay] Failed to list run replays: {ex.Message}");
            return;
        }

        for (int i = keep; i < files.Length; i++) {
            try {
                files[i].Delete();
            }
            catch (Exception ex) {
                GD.PrintErr($"[KitLib.RunReplay] Failed to delete {files[i].Name}: {ex.Message}");
            }
        }
    }

    static bool IsRunReplayFile(FileInfo file) {
        string ext = file.Extension;
        return ext.Equals(CombatReplayPlayback.RunReplayExtension, StringComparison.OrdinalIgnoreCase)
            || ext.Equals(CombatReplayPlayback.LegacyRunReplayExtension, StringComparison.OrdinalIgnoreCase);
    }
}
