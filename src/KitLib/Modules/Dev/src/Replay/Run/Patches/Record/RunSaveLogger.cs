using System;
using System.IO;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.Replay.Patches.Record;

using KitLib.Replay;
using KitLib.Replay.Utils;

/// <summary>
/// Harmony postfix on RunManager.ToSave() that writes one <c>.replay</c> for the run.
/// Same StartTime always overwrites that run's file (never a new file per launch).
/// Save-point markers let a later load of an earlier save rewind the log.
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
public static class RunSaveLogger {
    [HarmonyPostfix]
    public static void Postfix(SerializableRun __result) {
        if (ReplayEngine.IsActive)
            return;

        try {
            WriteRunLog(__result);
        }
        catch (Exception ex) {
            GD.PrintErr($"[KitLib.RunReplay] Failed to write run save log: {ex}");
        }
    }

    private static void WriteRunLog(SerializableRun run) {
        string seed = run.SerializableRng?.Seed ?? "unknown-seed";
        string character = run.Players?.FirstOrDefault()?.CharacterId?.Entry ?? "unknown-character";
        string path = CombatReplayPlayback.FilePathForRun(seed, character, run.StartTime);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var minimalActions = PlayerActionBuffer.SnapshotMinimal();

        // Act order is not derivable from the seed alone (the game's act roll
        // depends on discovery progress and a non-seed rng), so persist the
        // actual acts for the replay to force.
        string acts = string.Join(", ",
            run.Acts?.Select(a => a.Id?.ToString()).Where(id => id != null)
            ?? Enumerable.Empty<string>());

        string gameVersion;
        try {
            gameVersion = ReleaseInfoManager.Instance?.ReleaseInfo?.Version ?? "unknown";
        }
        catch {
            gameVersion = "unknown";
        }

        var existing = ReplayFormat.TryReadFile(path);
        var savePoints = ReplayFormat.MergeSavePoint(
            existing?.SavePoints, run.SaveTime, minimalActions.Count);

        string text = ReplayFormat.Format(
            ReplayFormat.CoreVersion,
            character,
            seed,
            run.Ascension,
            acts,
            gameVersion,
            run.StartTime,
            savePoints,
            minimalActions);
        File.WriteAllText(path, text);
        RunReplayRetention.Prune();

        GD.Print($"[KitLib.RunReplay] Wrote run replay: {path}");
        DiagnosticLog.Write("Save",
            $"ToSave — seed='{seed}' character={character} ascension={run.Ascension} " +
            $"gameMode={run.GameMode} saveTime={run.SaveTime} actions={minimalActions.Count} " +
            $"savePoints={savePoints.Count} file={path}");
    }
}
