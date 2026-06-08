using System;
using System.Linq;
using System.Threading.Tasks;
using KitLib.Actions;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Commands;

public class DmCardConsoleCmd : AbstractConsoleCmd {
    public override string CmdName => "dmcard";
    public override string Args => "<add|list> [cardId] [deck|hand|draw|discard|exhaust] [perm|temp]";
    public override string Description => "[KitLib] Add cards or list all card IDs";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    private static readonly string[] SubCmds = { "add", "list" };
    private static readonly string[] Targets = { "deck", "hand", "draw", "discard", "exhaust" };
    private static readonly string[] Durations = { "perm", "temp" };

    public override CmdResult Process(Player? issuingPlayer, string[] args) {
        if (args.Length < 1)
            return new CmdResult(false, "Usage: dmcard <add|list> [cardId] [deck|hand|draw|discard] [perm|temp]");

        var sub = args[0].ToLowerInvariant();

        switch (sub) {
            case "list": {
                    var cards = ModelDb.AllCards.ToList();
                    var names = cards.Select(c => ((AbstractModel)c).Id.Entry).OrderBy(n => n);
                    return new CmdResult(true, $"Cards ({cards.Count}):\n{string.Join(", ", names)}");
                }
            case "add": {
                    if (args.Length < 2)
                        return new CmdResult(false, "Usage: dmcard add <cardId> [deck|hand|draw|discard] [perm|temp]");

                    var cardId = args[1];
                    var card = ModelDb.AllCards.FirstOrDefault(c =>
                        string.Equals(((AbstractModel)c).Id.Entry, cardId, StringComparison.OrdinalIgnoreCase));
                    if (card == null)
                        return new CmdResult(false, $"Card not found: '{cardId}'");

                    if (!RunContext.TryGetRunAndPlayer(out var state, out var player))
                        return new CmdResult(false, "No active run.");

                    // Parse target
                    if (args.Length >= 3) {
                        KitLibState.CardTarget = args[2].ToLowerInvariant() switch {
                            "hand" => CardTarget.Hand,
                            "draw" => CardTarget.DrawPile,
                            "discard" => CardTarget.DiscardPile,
                            "exhaust" => CardTarget.ExhaustPile,
                            _ => CardTarget.Deck
                        };
                    }

                    // Parse duration
                    if (args.Length >= 4) {
                        KitLibState.EffectDuration = args[3].ToLowerInvariant() switch {
                            "temp" or "temporary" => EffectDuration.Temporary,
                            _ => EffectDuration.Permanent
                        };
                    }

                    TaskHelper.RunSafely(CardActions.Add(state, player, card).RunAsync());
                    return new CmdResult(true, $"Added '{cardId}' to {KitLibState.CardTarget} ({KitLibState.EffectDuration})");
                }
            default:
                return new CmdResult(false, $"Unknown subcommand: '{sub}'. Use: add, list");
        }
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) {
        if (args.Length <= 1)
            return CompleteArgument(SubCmds, Array.Empty<string>(), args.FirstOrDefault() ?? "");

        var sub = args[0].ToLowerInvariant();
        if (sub == "add") {
            if (args.Length == 2) {
                var cardIds = ModelDb.AllCards.Select(c => ((AbstractModel)c).Id.Entry).ToList();
                return CompleteArgument(cardIds, new[] { args[0] }, args[1]);
            }
            if (args.Length == 3)
                return CompleteArgument(Targets, new[] { args[0], args[1] }, args[2]);
            if (args.Length == 4)
                return CompleteArgument(Durations, new[] { args[0], args[1], args[2] }, args[3]);
        }

        return base.GetArgumentCompletions(player, args);
    }
}
