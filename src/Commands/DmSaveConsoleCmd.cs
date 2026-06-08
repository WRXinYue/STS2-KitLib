using System;
using System.Linq;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Commands;

public class DmSaveConsoleCmd : AbstractConsoleCmd {
    public override string CmdName => "dmsave";
    public override string Args => "<quick|load|slot|delete|list> [slotNumber] [name]";
    public override string Description => "[KitLib] Quick save/load, slot save/load/delete";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    private static readonly string[] SubCmds = { "quick", "load", "slot", "delete", "list" };

    public override CmdResult Process(Player? issuingPlayer, string[] args) {
        if (args.Length < 1)
            return new CmdResult(false, "Usage: dmsave <quick|load|slot|delete|list> [slotNumber] [name]");

        var sub = args[0].ToLowerInvariant();

        switch (sub) {
            case "quick": {
                    if (SaveSlotManager.QuickSave())
                        return new CmdResult(true, "Quick save successful.");
                    return new CmdResult(false, "Quick save failed. Is a run in progress?");
                }
            case "load": {
                    int slot = 0;
                    if (args.Length >= 2 && int.TryParse(args[1], out var s))
                        slot = s;

                    if (!SaveSlotManager.HasSlot(slot))
                        return new CmdResult(false, $"Slot {slot} is empty.");

                    if (SaveSlotManager.LoadFromSlot(slot))
                        return new CmdResult(true, $"Loading from slot {slot}...");
                    return new CmdResult(false, $"Failed to load slot {slot}.");
                }
            case "slot": {
                    if (args.Length < 2 || !int.TryParse(args[1], out var slot))
                        return new CmdResult(false, "Usage: dmsave slot <number> [name]");

                    if (slot < 0)
                        return new CmdResult(false, "Slot number must be >= 0.");

                    var name = args.Length >= 3 ? string.Join(" ", args.Skip(2)) : "";
                    if (SaveSlotManager.SaveToSlot(slot, name))
                        return new CmdResult(true, $"Saved to slot {slot}" + (string.IsNullOrEmpty(name) ? "." : $" ({name})."));
                    return new CmdResult(false, $"Failed to save to slot {slot}. Is a run in progress?");
                }
            case "delete": {
                    if (args.Length < 2 || !int.TryParse(args[1], out var slot))
                        return new CmdResult(false, "Usage: dmsave delete <slotNumber>");

                    if (SaveSlotManager.DeleteSlot(slot))
                        return new CmdResult(true, $"Deleted slot {slot}.");
                    return new CmdResult(false, $"Slot {slot} does not exist or could not be deleted.");
                }
            case "list": {
                    var ids = SaveSlotManager.GetAllSlotIds();
                    if (ids.Count == 0)
                        return new CmdResult(true, "No save slots found.");
                    var lines = ids.Select(id => {
                        var meta = SaveSlotManager.LoadMeta(id);
                        return meta != null
                        ? $"  [{id}] {meta.DisplayName}  F{meta.TotalFloor}  {meta.FormattedTime}"
                        : $"  [{id}] (data only, no meta)";
                    });
                    return new CmdResult(true, "Save slots:\n" + string.Join("\n", lines));
                }
            default:
                return new CmdResult(false, $"Unknown subcommand: '{sub}'. Use: quick, load, slot, delete, list");
        }
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) {
        if (args.Length <= 1)
            return CompleteArgument(SubCmds, Array.Empty<string>(), args.FirstOrDefault() ?? "");

        var sub = args[0].ToLowerInvariant();
        if ((sub == "load" || sub == "slot" || sub == "delete") && args.Length == 2) {
            var slots = SaveSlotManager.GetAllSlotIds().Select(i => i.ToString()).ToList();
            return CompleteArgument(slots, new[] { args[0] }, args[1]);
        }

        return base.GetArgumentCompletions(player, args);
    }
}
