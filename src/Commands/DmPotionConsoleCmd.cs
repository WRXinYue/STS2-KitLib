using System;
using System.Linq;
using KitLib.Actions;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Commands;

public class DmPotionConsoleCmd : AbstractConsoleCmd {
    public override string CmdName => "dmpotion";
    public override string Args => "<add|list> [potionId]";
    public override string Description => "[KitLib] Add potions or list all potion IDs";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    private static readonly string[] SubCmds = { "add", "list" };

    public override CmdResult Process(Player? issuingPlayer, string[] args) {
        if (args.Length < 1)
            return new CmdResult(false, "Usage: dmpotion <add|list> [potionId]");

        var sub = args[0].ToLowerInvariant();

        switch (sub) {
            case "list": {
                    var potions = PotionActions.GetAllPotions().ToList();
                    var names = potions.Select(p => ((AbstractModel)p).Id.Entry).OrderBy(n => n);
                    return new CmdResult(true, $"Potions ({potions.Count}):\n{string.Join(", ", names)}");
                }
            case "add": {
                    if (args.Length < 2)
                        return new CmdResult(false, "Usage: dmpotion add <potionId>");

                    var potionId = args[1];
                    var potion = PotionActions.GetAllPotions().FirstOrDefault(p =>
                        string.Equals(((AbstractModel)p).Id.Entry, potionId, StringComparison.OrdinalIgnoreCase));
                    if (potion == null)
                        return new CmdResult(false, $"Potion not found: '{potionId}'");

                    if (!RunContext.TryGetRunAndPlayer(out _, out var player))
                        return new CmdResult(false, "No active run.");

                    TaskHelper.RunSafely(PotionActions.AddPotion(player, potion));
                    return new CmdResult(true, $"Added potion: {potionId}");
                }
            default:
                return new CmdResult(false, $"Unknown subcommand: '{sub}'. Use: add, list");
        }
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) {
        if (args.Length <= 1)
            return CompleteArgument(SubCmds, Array.Empty<string>(), args.FirstOrDefault() ?? "");

        if (args[0].Equals("add", StringComparison.OrdinalIgnoreCase) && args.Length == 2) {
            var ids = PotionActions.GetAllPotions().Select(p => ((AbstractModel)p).Id.Entry).ToList();
            return CompleteArgument(ids, new[] { args[0] }, args[1]);
        }

        return base.GetArgumentCompletions(player, args);
    }
}
