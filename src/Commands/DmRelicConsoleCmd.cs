using System;
using System.Linq;
using KitLib.Actions;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Commands;

public class DmRelicConsoleCmd : AbstractConsoleCmd {
    public override string CmdName => "dmrelic";
    public override string Args => "<add|list> [relicId]";
    public override string Description => "[KitLib] Add relics or list all relic IDs";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    private static readonly string[] SubCmds = { "add", "list" };

    public override CmdResult Process(Player? issuingPlayer, string[] args) {
        if (args.Length < 1)
            return new CmdResult(false, "Usage: dmrelic <add|list> [relicId]");

        var sub = args[0].ToLowerInvariant();

        switch (sub) {
            case "list": {
                    var relics = ModelDb.AllRelics.ToList();
                    var names = relics.Select(r => ((AbstractModel)r).Id.Entry).OrderBy(n => n);
                    return new CmdResult(true, $"Relics ({relics.Count}):\n{string.Join(", ", names)}");
                }
            case "add": {
                    if (args.Length < 2)
                        return new CmdResult(false, "Usage: dmrelic add <relicId>");

                    var relicId = args[1];
                    var relic = ModelDb.AllRelics.FirstOrDefault(r =>
                        string.Equals(((AbstractModel)r).Id.Entry, relicId, StringComparison.OrdinalIgnoreCase));
                    if (relic == null)
                        return new CmdResult(false, $"Relic not found: '{relicId}'");

                    if (!RunContext.TryGetRunAndPlayer(out _, out var player))
                        return new CmdResult(false, "No active run.");

                    TaskHelper.RunSafely(RelicActions.AddRelic(relic, player));
                    return new CmdResult(true, $"Added relic: {relicId}");
                }
            default:
                return new CmdResult(false, $"Unknown subcommand: '{sub}'. Use: add, list");
        }
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) {
        if (args.Length <= 1)
            return CompleteArgument(SubCmds, Array.Empty<string>(), args.FirstOrDefault() ?? "");

        if (args[0].Equals("add", StringComparison.OrdinalIgnoreCase) && args.Length == 2) {
            var relicIds = ModelDb.AllRelics.Select(r => ((AbstractModel)r).Id.Entry).ToList();
            return CompleteArgument(relicIds, new[] { args[0] }, args[1]);
        }

        return base.GetArgumentCompletions(player, args);
    }
}
