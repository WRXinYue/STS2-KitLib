using System;
using System.Linq;
using KitLib.Cheat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Commands;

public class DmRuntimeConsoleCmd : AbstractConsoleCmd {
    public override string CmdName => "dmruntime";
    public override string Args => "<toggle> [on|off|value]";
    public override string Description => "[KitLib] Runtime stat modifiers and stat locks";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    private static readonly string[] Toggles =
    {
        "godmode", "killall", "energy", "playerturn", "drawtolimit",
        "extradraw", "autoally", "negatedebuffs"
    };

    private static readonly string[] Locks =
    {
        "lockgold", "lockhp", "lockmaxhp", "lockenergy", "lockmaxenergy",
        "lockstars", "lockorbslots"
    };

    private static readonly string[] AllSubs = Toggles.Concat(Locks).Append("status").ToArray();

    private static RuntimeStatModifiers EnsureMods() {
        CheatRunState.StatModifiers ??= new RuntimeStatModifiers();
        return CheatRunState.StatModifiers;
    }

    public override CmdResult Process(Player? issuingPlayer, string[] args) {
        if (args.Length < 1)
            return new CmdResult(false, $"Usage: dmruntime <toggle> [on|off|value]\nToggles: {string.Join(", ", Toggles)}\nLocks: {string.Join(", ", Locks)}");

        var sub = args[0].ToLowerInvariant();
        bool? flag = args.Length >= 2 ? ParseBool(args[1]) : null;
        var m = EnsureMods();

        switch (sub) {
            case "godmode": {
                    m.GodMode = flag ?? !m.GodMode;
                    return new CmdResult(true, $"God Mode: {OnOff(m.GodMode)}");
                }
            case "killall": {
                    m.KillAllEnemies = flag ?? !m.KillAllEnemies;
                    return new CmdResult(true, $"Kill All Enemies: {OnOff(m.KillAllEnemies)}");
                }
            case "energy": {
                    m.InfiniteEnergy = flag ?? !m.InfiniteEnergy;
                    return new CmdResult(true, $"Infinite Energy (Runtime): {OnOff(m.InfiniteEnergy)}");
                }
            case "playerturn": {
                    m.AlwaysPlayerTurn = flag ?? !m.AlwaysPlayerTurn;
                    return new CmdResult(true, $"Always Player Turn: {OnOff(m.AlwaysPlayerTurn)}");
                }
            case "drawtolimit": {
                    m.DrawToHandLimit = flag ?? !m.DrawToHandLimit;
                    return new CmdResult(true, $"Draw to Hand Limit: {OnOff(m.DrawToHandLimit)}");
                }
            case "extradraw": {
                    if (args.Length >= 2 && int.TryParse(args[1], out var amount)) {
                        m.ExtraDrawEachTurn = true;
                        m.ExtraDrawEachTurnAmount = Math.Clamp(amount, 1, 20);
                        return new CmdResult(true, $"Extra Draw Each Turn: ON ({m.ExtraDrawEachTurnAmount} cards)");
                    }
                    m.ExtraDrawEachTurn = flag ?? !m.ExtraDrawEachTurn;
                    return new CmdResult(true, $"Extra Draw Each Turn: {OnOff(m.ExtraDrawEachTurn)} ({m.ExtraDrawEachTurnAmount} cards)");
                }
            case "autoally": {
                    m.AutoActFriendlyMonsters = flag ?? !m.AutoActFriendlyMonsters;
                    return new CmdResult(true, $"Auto-Act Friendly Monsters: {OnOff(m.AutoActFriendlyMonsters)}");
                }
            case "negatedebuffs": {
                    m.NegateDebuffs = flag ?? !m.NegateDebuffs;
                    return new CmdResult(true, $"Negate Debuffs: {OnOff(m.NegateDebuffs)}");
                }

            // Stat locks
            case "lockgold":
                return HandleLock(args, "Lock Gold", () => m.LockGold, v => m.LockGold = v, () => m.LockedGoldValue, v => m.LockedGoldValue = v);
            case "lockhp":
                return HandleLock(args, "Lock Current HP", () => m.LockCurrentHp, v => m.LockCurrentHp = v, () => m.LockedCurrentHpValue, v => m.LockedCurrentHpValue = v);
            case "lockmaxhp":
                return HandleLock(args, "Lock Max HP", () => m.LockMaxHp, v => m.LockMaxHp = v, () => m.LockedMaxHpValue, v => m.LockedMaxHpValue = v);
            case "lockenergy":
                return HandleLock(args, "Lock Current Energy", () => m.LockCurrentEnergy, v => m.LockCurrentEnergy = v, () => m.LockedCurrentEnergyValue, v => m.LockedCurrentEnergyValue = v);
            case "lockmaxenergy":
                return HandleLock(args, "Lock Max Energy", () => m.LockMaxEnergy, v => m.LockMaxEnergy = v, () => m.LockedMaxEnergyValue, v => m.LockedMaxEnergyValue = v);
            case "lockstars":
                return HandleLock(args, "Lock Stars", () => m.LockStars, v => m.LockStars = v, () => m.LockedStarsValue, v => m.LockedStarsValue = v);
            case "lockorbslots":
                return HandleLock(args, "Lock Orb Slots", () => m.LockOrbSlots, v => m.LockOrbSlots = v, () => m.LockedOrbSlotsValue, v => m.LockedOrbSlotsValue = v);

            case "status":
                return ShowStatus(m);

            default:
                return new CmdResult(false, $"Unknown toggle: '{sub}'. Available: {string.Join(", ", AllSubs)}");
        }
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args) {
        if (args.Length <= 1)
            return CompleteArgument(AllSubs, Array.Empty<string>(), args.FirstOrDefault() ?? "");

        var sub = args[0].ToLowerInvariant();
        if (Toggles.Contains(sub))
            return CompleteArgument(new[] { "on", "off" }, new[] { args[0] }, args.Length > 1 ? args[1] : "");

        return base.GetArgumentCompletions(player, args);
    }

    private static CmdResult HandleLock(string[] args, string label, Func<bool> getEnabled, Action<bool> setEnabled, Func<int> getValue, Action<int> setValue) {
        if (args.Length >= 2 && int.TryParse(args[1], out var val)) {
            setValue(val);
            setEnabled(true);
            return new CmdResult(true, $"{label}: ON (locked at {val})");
        }

        bool? flag = args.Length >= 2 ? ParseBool(args[1]) : null;
        var newVal = flag ?? !getEnabled();
        setEnabled(newVal);
        return new CmdResult(true, $"{label}: {OnOff(newVal)}" + (newVal ? $" (value: {getValue()})" : ""));
    }

    private static CmdResult ShowStatus(RuntimeStatModifiers m) {
        var lines = new[]
        {
            $"God Mode: {OnOff(m.GodMode)}",
            $"Kill All: {OnOff(m.KillAllEnemies)}",
            $"Infinite Energy: {OnOff(m.InfiniteEnergy)}",
            $"Always Player Turn: {OnOff(m.AlwaysPlayerTurn)}",
            $"Draw to Limit: {OnOff(m.DrawToHandLimit)}",
            $"Extra Draw: {OnOff(m.ExtraDrawEachTurn)} ({m.ExtraDrawEachTurnAmount})",
            $"Auto Ally: {OnOff(m.AutoActFriendlyMonsters)}",
            $"Negate Debuffs: {OnOff(m.NegateDebuffs)}",
            $"Lock Gold: {OnOff(m.LockGold)} ({m.LockedGoldValue})",
            $"Lock HP: {OnOff(m.LockCurrentHp)} ({m.LockedCurrentHpValue})",
            $"Lock Max HP: {OnOff(m.LockMaxHp)} ({m.LockedMaxHpValue})",
            $"Lock Energy: {OnOff(m.LockCurrentEnergy)} ({m.LockedCurrentEnergyValue})",
            $"Lock Max Energy: {OnOff(m.LockMaxEnergy)} ({m.LockedMaxEnergyValue})",
            $"Lock Stars: {OnOff(m.LockStars)} ({m.LockedStarsValue})",
            $"Lock Orb Slots: {OnOff(m.LockOrbSlots)} ({m.LockedOrbSlotsValue})",
        };
        return new CmdResult(true, string.Join("\n", lines));
    }

    private static string OnOff(bool v) => v ? "ON" : "OFF";

    private static bool? ParseBool(string s) => s.ToLowerInvariant() switch {
        "on" or "true" or "1" or "yes" => true,
        "off" or "false" or "0" or "no" => false,
        _ => null
    };
}
