using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Actions;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.Commands;

public class DmEnemyConsoleCmd : AbstractConsoleCmd {
    public override string CmdName => "dmenemy";
    public override string Args => "<set|clear|spawn|kill|list> [id|all]";
    public override string Description => "[KitLib] Enemy/encounter manipulation";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    private static readonly string[] SubCmds = { "set", "clear", "spawn", "kill", "list", "listmonsters" };

    private static IReadOnlyList<MonsterModel> GetAllMonsters() {
        return ModelDb.AllEncounters
            .SelectMany(e => e.AllPossibleMonsters ?? Enumerable.Empty<MonsterModel>())
            .GroupBy(m => ((AbstractModel)m).Id.Entry)
            .Select(g => g.First())
            .OrderBy(m => ((AbstractModel)m).Id.Entry)
            .ToList();
    }

    public override CmdResult Process(Player? issuingPlayer, string[] args) {
        if (args.Length < 1)
            return new CmdResult(false, "Usage: dmenemy <set|clear|spawn|kill|list|listmonsters> [id|all]");

        var sub = args[0].ToLowerInvariant();

        switch (sub) {
            case "list": {
                    var encounters = EnemyActions.GetAllEncounters();
                    var names = encounters.Select(e => ((AbstractModel)e).Id.Entry);
                    return new CmdResult(true, $"Encounters ({encounters.Count}):\n{string.Join(", ", names)}");
                }
            case "listmonsters": {
                    var monsters = GetAllMonsters();
                    var names = monsters.Select(m => ((AbstractModel)m).Id.Entry);
                    return new CmdResult(true, $"Monsters ({monsters.Count}):\n{string.Join(", ", names)}");
                }
            case "set": {
                    if (args.Length < 2)
                        return new CmdResult(false, "Usage: dmenemy set <encounterId>");

                    var encId = args[1];
                    var encounter = ModelDb.AllEncounters.FirstOrDefault(e =>
                        string.Equals(((AbstractModel)e).Id.Entry, encId, StringComparison.OrdinalIgnoreCase));
                    if (encounter == null)
                        return new CmdResult(false, $"Encounter not found: '{encId}'");

                    EnemyActions.SetGlobalOverride(encounter);
                    return new CmdResult(true, $"Global encounter override set to: {encId}");
                }
            case "clear": {
                    EnemyActions.ClearAll();
                    return new CmdResult(true, "All enemy overrides cleared.");
                }
            case "spawn": {
                    if (args.Length < 2)
                        return new CmdResult(false, "Usage: dmenemy spawn <monsterId>");

                    var monsterId = args[1];
                    var monster = GetAllMonsters().FirstOrDefault(m =>
                        string.Equals(((AbstractModel)m).Id.Entry, monsterId, StringComparison.OrdinalIgnoreCase));
                    if (monster == null)
                        return new CmdResult(false, $"Monster not found: '{monsterId}'");

                    TaskHelper.RunSafely(CombatEnemyActions.AddMonster(monster));
                    return new CmdResult(true, $"Spawning monster: {monsterId}");
                }
            case "kill": {
                    var target = args.Length >= 2 ? args[1].ToLowerInvariant() : "all";
                    if (target == "all") {
                        TaskHelper.RunSafely(CombatEnemyActions.KillAllEnemies());
                        return new CmdResult(true, "Killing all enemies.");
                    }

                    if (int.TryParse(target, out var index)) {
                        var enemies = CombatEnemyActions.GetCurrentEnemies();
                        if (index < 0 || index >= enemies.Count)
                            return new CmdResult(false, $"Invalid index. Current enemies: 0-{enemies.Count - 1}");
                        TaskHelper.RunSafely(CombatEnemyActions.KillEnemy(enemies[index]));
                        return new CmdResult(true, $"Killing enemy at index {index}.");
                    }

                    return new CmdResult(false, "Usage: dmenemy kill [all|index]");
                }
            default:
                return new CmdResult(false, $"Unknown subcommand: '{sub}'. Use: {string.Join(", ", SubCmds)}");
        }
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) {
        if (args.Length <= 1)
            return CompleteArgument(SubCmds, Array.Empty<string>(), args.FirstOrDefault() ?? "");

        var sub = args[0].ToLowerInvariant();

        if (sub == "set" && args.Length == 2) {
            var ids = ModelDb.AllEncounters.Select(e => ((AbstractModel)e).Id.Entry).ToList();
            return CompleteArgument(ids, new[] { args[0] }, args[1]);
        }

        if (sub == "spawn" && args.Length == 2) {
            var ids = GetAllMonsters().Select(m => ((AbstractModel)m).Id.Entry).ToList();
            return CompleteArgument(ids, new[] { args[0] }, args[1]);
        }

        if (sub == "kill" && args.Length == 2) {
            var options = new[] { "all" }.Concat(
                Enumerable.Range(0, CombatEnemyActions.GetCurrentEnemies().Count).Select(i => i.ToString())).ToList();
            return CompleteArgument(options, new[] { args[0] }, args[1]);
        }

        return base.GetArgumentCompletions(player, args);
    }
}
