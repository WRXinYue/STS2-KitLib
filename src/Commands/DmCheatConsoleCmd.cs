using System;
using System.Linq;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Commands;

public class DmCheatConsoleCmd : AbstractConsoleCmd {
    public override string CmdName => "dmcheat";
    public override string Args => "<toggle> [on|off|value]";
    public override string Description => "[KitLib] Toggle cheat flags (godmode, infinitehp, onehitkill, ...)";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    private static readonly string[] Toggles =
    {
        "godmode", "infinitehp", "infiniteblock", "infiniteenergy", "infinitestars",
        "freezeenemies", "onehitkill", "freeshop", "alwayspotion", "alwaysupgrade",
        "maxrarity", "unknowntreasure", "maxscore"
    };

    private static readonly string[] Multipliers =
    {
        "damagemult", "defensemult", "goldmult", "scoremult"
    };

    private static readonly string[] AllSubs = Toggles.Concat(Multipliers).ToArray();

    public override CmdResult Process(Player? issuingPlayer, string[] args) {
        if (args.Length < 1)
            return new CmdResult(false, $"Usage: dmcheat <toggle> [on|off|value]\nToggles: {string.Join(", ", Toggles)}\nMultipliers: {string.Join(", ", Multipliers)}");

        var sub = args[0].ToLowerInvariant();
        bool? flag = args.Length >= 2 ? ParseBool(args[1]) : null;

        switch (sub) {
            case "godmode":
            case "infinitehp": {
                    var v = flag ?? !KitLibState.PlayerCheats.InfiniteHp;
                    KitLibState.PlayerCheats.InfiniteHp = v;
                    return new CmdResult(true, $"Infinite HP: {OnOff(v)}");
                }
            case "infiniteblock": {
                    var v = flag ?? !KitLibState.PlayerCheats.InfiniteBlock;
                    KitLibState.PlayerCheats.InfiniteBlock = v;
                    return new CmdResult(true, $"Infinite Block: {OnOff(v)}");
                }
            case "infiniteenergy": {
                    var v = flag ?? !KitLibState.PlayerCheats.InfiniteEnergy;
                    KitLibState.PlayerCheats.InfiniteEnergy = v;
                    if (v) PlayerCheatEffects.ApplyImmediateIfEnabled();
                    return new CmdResult(true, $"Infinite Energy: {OnOff(v)}");
                }
            case "infinitestars": {
                    var v = flag ?? !KitLibState.PlayerCheats.InfiniteStars;
                    KitLibState.PlayerCheats.InfiniteStars = v;
                    if (v) PlayerCheatEffects.ApplyImmediateIfEnabled();
                    return new CmdResult(true, $"Infinite Stars: {OnOff(v)}");
                }
            case "freezeenemies": {
                    var v = flag ?? !KitLibState.EnemyCheats.FreezeEnemies;
                    KitLibState.EnemyCheats.FreezeEnemies = v;
                    return new CmdResult(true, $"Freeze Enemies: {OnOff(v)}");
                }
            case "onehitkill": {
                    var v = flag ?? !KitLibState.EnemyCheats.OneHitKill;
                    KitLibState.EnemyCheats.OneHitKill = v;
                    return new CmdResult(true, $"One-Hit Kill: {OnOff(v)}");
                }
            case "freeshop": {
                    var v = flag ?? !KitLibState.GameplayModifiers.FreeShop;
                    KitLibState.GameplayModifiers.FreeShop = v;
                    return new CmdResult(true, $"Free Shop: {OnOff(v)}");
                }
            case "alwayspotion": {
                    var v = flag ?? !KitLibState.PlayerCheats.AlwaysRewardPotion;
                    KitLibState.PlayerCheats.AlwaysRewardPotion = v;
                    return new CmdResult(true, $"Always Reward Potion: {OnOff(v)}");
                }
            case "alwaysupgrade": {
                    var v = flag ?? !KitLibState.PlayerCheats.AlwaysUpgradeCardReward;
                    KitLibState.PlayerCheats.AlwaysUpgradeCardReward = v;
                    return new CmdResult(true, $"Always Upgrade Reward: {OnOff(v)}");
                }
            case "maxrarity": {
                    var v = flag ?? !KitLibState.PlayerCheats.MaxCardRewardRarity;
                    KitLibState.PlayerCheats.MaxCardRewardRarity = v;
                    return new CmdResult(true, $"Max Card Reward Rarity: {OnOff(v)}");
                }
            case "unknowntreasure": {
                    var v = flag ?? !KitLibState.MapCheats.UnknownMapAlwaysTreasure;
                    KitLibState.MapCheats.UnknownMapAlwaysTreasure = v;
                    return new CmdResult(true, $"Unknown → Treasure: {OnOff(v)}");
                }
            case "maxscore": {
                    var v = flag ?? !KitLibState.GameplayModifiers.MaxScore;
                    KitLibState.GameplayModifiers.MaxScore = v;
                    return new CmdResult(true, $"Max Score: {OnOff(v)}");
                }

            // Multipliers
            case "damagemult":
                return SetMultiplier(args, "Damage Multiplier", KitLibState.EnemyCheats.DamageMultiplier, v => KitLibState.EnemyCheats.DamageMultiplier = v);
            case "defensemult":
                return SetMultiplier(args, "Defense Multiplier", KitLibState.PlayerCheats.DefenseMultiplier, v => KitLibState.PlayerCheats.DefenseMultiplier = v);
            case "goldmult":
                return SetMultiplier(args, "Gold Multiplier", KitLibState.GameplayModifiers.GoldMultiplier, v => KitLibState.GameplayModifiers.GoldMultiplier = v);
            case "scoremult":
                return SetMultiplier(args, "Score Multiplier", KitLibState.GameplayModifiers.ScoreMultiplier, v => KitLibState.GameplayModifiers.ScoreMultiplier = v);

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

    private static CmdResult SetMultiplier(string[] args, string label, float current, Action<float> setter) {
        if (args.Length < 2)
            return new CmdResult(true, $"{label}: {current:F1}");
        if (!float.TryParse(args[1], out var val) || val < 0)
            return new CmdResult(false, $"Invalid value. Usage: dmcheat {args[0]} <float>");
        setter(val);
        return new CmdResult(true, $"{label}: {val:F1}");
    }

    private static string OnOff(bool v) => v ? "ON" : "OFF";

    private static bool? ParseBool(string s) => s.ToLowerInvariant() switch {
        "on" or "true" or "1" or "yes" => true,
        "off" or "false" or "0" or "no" => false,
        _ => null
    };
}
