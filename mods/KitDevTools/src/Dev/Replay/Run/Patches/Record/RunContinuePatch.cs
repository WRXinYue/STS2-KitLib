using System;
using System.IO;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.Replay.Patches.Record;

using KitLib.Replay;

/// <summary>
/// Restores the action buffer when a save is loaded so later ToSave calls
/// keep appending to that run's <c>.replay</c>.
///
/// Patches <c>InitializeSavedRun</c> (after ActionExecutor construction) rather
/// than the async SetUpSaved* methods, whose Harmony postfix can run before
/// the buffer is cleared.
///
/// Save-scum: restore only the prefix tagged for this save's SaveTime. The
/// file is not rewritten until the next ToSave, so a later save of the same
/// run still sees the longer log until that rewind is saved over.
/// </summary>
[HarmonyPatch(typeof(RunManager), "InitializeSavedRun")]
public static class RunContinuePatch {
    [HarmonyPostfix]
    public static void Postfix(SerializableRun save) {
        try {
            RestoreActionBuffer(save);
        }
        catch (Exception ex) {
            GD.PrintErr($"[KitLib.RunReplay] Failed to restore action buffer on continue: {ex}");
        }
    }

    private static void RestoreActionBuffer(SerializableRun save) {
        if (save == null)
            return;
        if (RunManager.Instance?.NetService is NetReplayGameService)
            return;
        if (ReplayEngine.IsReplayRun)
            return;

        string seed = save.SerializableRng?.Seed ?? "unknown-seed";
        string character = save.Players?.FirstOrDefault()?.CharacterId?.Entry ?? "unknown-character";
        string? path = CombatReplayPlayback.FindExistingRunReplay(seed, character, save.StartTime);
        if (path == null) {
            GD.Print($"[KitLib.RunReplay] No run replay found for seed '{seed}'");
            return;
        }

        var log = ReplayFormat.Parse(File.ReadAllLines(path));
        if (!ReplayFormat.IsPlayable(log.CoreVersion)) {
            GD.PrintErr(
                $"[KitLib.RunReplay] ReplayCore {log.CoreVersion} is not playable " +
                $"(supported {ReplayFormat.MinSupportedCore}–{ReplayFormat.CoreVersion}); not restoring.");
            return;
        }

        int keep = log.CommandCountForSave(save.SaveTime);
        var minimalEntries = log.Commands.Take(keep).ToList();
        var verboseEntries = minimalEntries.Select(e => ("RESTORED", e)).ToList();
        PlayerActionBuffer.Restore(verboseEntries, minimalEntries);
        RunOverlay.RestoreRecentEntries(minimalEntries);
        GD.Print(
            $"[KitLib.RunReplay] Restored {minimalEntries.Count}/{log.Commands.Count} entries " +
            $"(saveTime={save.SaveTime}) from: {path}");
    }
}
