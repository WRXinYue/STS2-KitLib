using System;
using System.Linq;
using KitLib.Actions;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Commands;

public class DmPowerConsoleCmd : AbstractConsoleCmd {
    public override string CmdName => "dmpower";
    public override string Args => "<add|list> [powerId] [amount] [self|enemies|allies]";
    public override string Description => "[KitLib] Apply powers or list all power IDs";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    private static readonly string[] SubCmds = { "add", "list" };
    private static readonly string[] TargetNames = { "self", "enemies", "allies" };

    public override CmdResult Process(Player? issuingPlayer, string[] args) {
        if (args.Length < 1)
            return new CmdResult(false, "Usage: dmpower <add|list> [powerId] [amount] [self|enemies|allies]");

        var sub = args[0].ToLowerInvariant();

        switch (sub) {
            case "list": {
                    var powers = PowerActions.GetAllPowers().ToList();
                    var names = powers.Select(p => ((AbstractModel)p).Id.Entry).OrderBy(n => n);
                    return new CmdResult(true, $"Powers ({powers.Count}):\n{string.Join(", ", names)}");
                }
            case "add": {
                    if (args.Length < 2)
                        return new CmdResult(false, "Usage: dmpower add <powerId> [amount=1] [self|enemies|allies]");

                    var powerId = args[1];
                    var power = PowerActions.GetAllPowers().FirstOrDefault(p =>
                        string.Equals(((AbstractModel)p).Id.Entry, powerId, StringComparison.OrdinalIgnoreCase));
                    if (power == null)
                        return new CmdResult(false, $"Power not found: '{powerId}'");

                    if (!RunContext.TryGetRunAndPlayer(out _, out var player))
                        return new CmdResult(false, "No active run.");

                    int amount = 1;
                    if (args.Length >= 3 && int.TryParse(args[2], out var a))
                        amount = a;

                    var target = PowerTarget.Self;
                    if (args.Length >= 4) {
                        target = args[3].ToLowerInvariant() switch {
                            "enemies" or "allenemy" or "allenemy" => PowerTarget.AllEnemies,
                            "allies" or "ally" => PowerTarget.Allies,
                            _ => PowerTarget.Self
                        };
                    }

                    TaskHelper.RunSafely(PowerActions.AddPower(player, power, amount, target));
                    return new CmdResult(true, $"Applied {powerId} x{amount} to {target}");
                }
            default:
                return new CmdResult(false, $"Unknown subcommand: '{sub}'. Use: add, list");
        }
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) {
        if (args.Length <= 1)
            return CompleteArgument(SubCmds, Array.Empty<string>(), args.FirstOrDefault() ?? "");

        if (args[0].Equals("add", StringComparison.OrdinalIgnoreCase)) {
            if (args.Length == 2) {
                var ids = PowerActions.GetAllPowers().Select(p => ((AbstractModel)p).Id.Entry).ToList();
                return CompleteArgument(ids, new[] { args[0] }, args[1]);
            }
            if (args.Length == 4)
                return CompleteArgument(TargetNames, new[] { args[0], args[1], args[2] }, args[3]);
        }

        return base.GetArgumentCompletions(player, args);
    }
}
