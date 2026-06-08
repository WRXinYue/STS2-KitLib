using System;
using System.IO;
using KitLib.CombatStats;
using Godot;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Commands;

public class DmStatsConsoleCmd : AbstractConsoleCmd {
    public override string CmdName => "dmstats";
    public override string Args => "[export [filename]] | [text]";
    public override string Description => "[KitLib] Dump or export combat statistics (current / last / run total)";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args) {
        if (!KitLibState.IsActive)
            return new CmdResult(false, "Dev Mode is not active.");

        if (args.Length == 0)
            return new CmdResult(true, CombatStatsExport.ToTextSummary(
                CombatStatsBundle.From(
                    CombatStatsTracker.IsTracking ? CombatStatsTracker.Current : null,
                    CombatStatsTracker.Last,
                    CombatStatsTracker.RunTotal,
                    CombatStatsTracker.RunCombatCount)));

        var sub = args[0].ToLowerInvariant();
        if (sub == "text")
            return new CmdResult(true, CombatStatsExport.ToTextSummary(CombatStatsExport.CaptureBundle()));

        if (sub == "export") {
            string fileName = args.Length >= 2 ? args[1] : $"combat-stats-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                fileName += ".json";

            string dir = Path.Combine(OS.GetUserDataDir(), "mod_data", "KitLib");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);

            var bundle = CombatStatsBundle.From(
                CombatStatsTracker.IsTracking ? CombatStatsTracker.Current : null,
                CombatStatsTracker.Last,
                CombatStatsTracker.RunTotal,
                CombatStatsTracker.RunCombatCount);
            File.WriteAllText(path, CombatStatsExport.ToJson(bundle));
            return new CmdResult(true, $"Exported combat stats to {path}");
        }

        return new CmdResult(false, "Usage: dmstats | dmstats text | dmstats export [filename]");
    }
}
