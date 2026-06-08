using System;
using System.Text.Json.Nodes;
using KitLib.Cheat;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Mcp.Tools;

internal static class DevCheatMcpHelper {
    public static JsonObject Fail(string error) => new() {
        ["ok"] = false,
        ["error"] = error,
    };

    public static bool TryRequireCheats(out JsonObject? error) {
        error = null;
        if (!KitLibState.CheatsInRun) {
            error = Fail("Cheats are not active. Start a dev test run or set NormalRunMode to Cheat.");
            return false;
        }
        return true;
    }

    public static RuntimeStatModifiers EnsureStatModifiers() {
        CheatRunState.StatModifiers ??= new RuntimeStatModifiers();
        return CheatRunState.StatModifiers;
    }

    public static bool? ParseOptionalBool(JsonObject args, out string? error) {
        error = null;
        if (!args.TryGetPropertyValue("enabled", out var node))
            return null;

        if (node?.GetValueKind() == System.Text.Json.JsonValueKind.True)
            return true;
        if (node?.GetValueKind() == System.Text.Json.JsonValueKind.False)
            return false;

        if (node?.GetValueKind() == System.Text.Json.JsonValueKind.String) {
            return node.GetValue<string>()!.Trim().ToLowerInvariant() switch {
                "on" or "true" or "1" or "yes" => true,
                "off" or "false" or "0" or "no" => false,
                _ => null,
            };
        }

        error = "Invalid enabled value. Use true/false or on/off.";
        return null;
    }

    public static bool TryParseCheatName(string? raw, out string cheat, out string error) {
        cheat = (raw ?? "").Trim().ToLowerInvariant().Replace('-', '_');
        error = "";
        if (string.IsNullOrEmpty(cheat)) {
            error = "Missing cheat name.";
            return false;
        }
        return true;
    }

    public static JsonObject? ApplyCheat(string cheat, bool? enabled, float? value) {
        if (MpCheatSession.InMultiplayerRun && IsRuntimeCheat(cheat))
            return Fail("Runtime cheats are not supported via MCP in multiplayer.");

        switch (cheat) {
            // Patch toggles
            case "infinite_hp":
            case "godmode": {
                    var v = enabled ?? !KitLibState.PlayerCheats.InfiniteHp;
                    KitLibState.PlayerCheats.InfiniteHp = v;
                    return Ok(cheat, v);
                }
            case "infinite_block": {
                    var v = enabled ?? !KitLibState.PlayerCheats.InfiniteBlock;
                    KitLibState.PlayerCheats.InfiniteBlock = v;
                    if (!MpCheatSession.InMultiplayerRun && v && RunContext.TryGetRunAndPlayer(out _, out var bp)) {
                        var c = bp.Creature;
                        if (c.Block < 999) c.GainBlockInternal(999 - c.Block);
                    }
                    return Ok(cheat, v);
                }
            case "infinite_energy": {
                    var v = enabled ?? !KitLibState.PlayerCheats.InfiniteEnergy;
                    KitLibState.PlayerCheats.InfiniteEnergy = v;
                    if (v) PlayerCheatEffects.ApplyImmediateIfEnabled();
                    return Ok(cheat, v);
                }
            case "infinite_stars": {
                    var v = enabled ?? !KitLibState.PlayerCheats.InfiniteStars;
                    KitLibState.PlayerCheats.InfiniteStars = v;
                    if (v) PlayerCheatEffects.ApplyImmediateIfEnabled();
                    return Ok(cheat, v);
                }
            case "freeze_enemies": {
                    var v = enabled ?? !KitLibState.EnemyCheats.FreezeEnemies;
                    KitLibState.EnemyCheats.FreezeEnemies = v;
                    return Ok(cheat, v);
                }
            case "one_hit_kill": {
                    var v = enabled ?? !KitLibState.EnemyCheats.OneHitKill;
                    KitLibState.EnemyCheats.OneHitKill = v;
                    return Ok(cheat, v);
                }
            case "free_shop": {
                    var v = enabled ?? !KitLibState.GameplayModifiers.FreeShop;
                    KitLibState.GameplayModifiers.FreeShop = v;
                    return Ok(cheat, v);
                }
            case "always_potion": {
                    var v = enabled ?? !KitLibState.PlayerCheats.AlwaysRewardPotion;
                    KitLibState.PlayerCheats.AlwaysRewardPotion = v;
                    return Ok(cheat, v);
                }
            case "always_upgrade": {
                    var v = enabled ?? !KitLibState.PlayerCheats.AlwaysUpgradeCardReward;
                    KitLibState.PlayerCheats.AlwaysUpgradeCardReward = v;
                    return Ok(cheat, v);
                }
            case "max_rarity": {
                    var v = enabled ?? !KitLibState.PlayerCheats.MaxCardRewardRarity;
                    KitLibState.PlayerCheats.MaxCardRewardRarity = v;
                    return Ok(cheat, v);
                }
            case "unknown_treasure": {
                    var v = enabled ?? !KitLibState.MapCheats.UnknownMapAlwaysTreasure;
                    KitLibState.MapCheats.UnknownMapAlwaysTreasure = v;
                    return Ok(cheat, v);
                }
            case "max_score": {
                    var v = enabled ?? !KitLibState.GameplayModifiers.MaxScore;
                    KitLibState.GameplayModifiers.MaxScore = v;
                    return Ok(cheat, v);
                }

            // Multipliers
            case "damage_multiplier":
                return ApplyMultiplier(cheat, value, KitLibState.EnemyCheats.DamageMultiplier,
                    v => KitLibState.EnemyCheats.DamageMultiplier = v);
            case "defense_multiplier":
                return ApplyMultiplier(cheat, value, KitLibState.PlayerCheats.DefenseMultiplier,
                    v => KitLibState.PlayerCheats.DefenseMultiplier = v);
            case "gold_multiplier":
                return ApplyMultiplier(cheat, value, KitLibState.GameplayModifiers.GoldMultiplier,
                    v => KitLibState.GameplayModifiers.GoldMultiplier = v);
            case "score_multiplier":
                return ApplyMultiplier(cheat, value, KitLibState.GameplayModifiers.ScoreMultiplier,
                    v => KitLibState.GameplayModifiers.ScoreMultiplier = v);
            case "game_speed":
                return ApplyMultiplier(cheat, value, KitLibState.GameplayModifiers.GameSpeed,
                    v => KitLibState.GameplayModifiers.GameSpeed = v);

            // Runtime frame cheats
            case "god_mode": {
                    var m = EnsureStatModifiers();
                    var v = enabled ?? !m.GodMode;
                    m.GodMode = v;
                    return Ok(cheat, v);
                }
            case "kill_all": {
                    var m = EnsureStatModifiers();
                    var v = enabled ?? !m.KillAllEnemies;
                    m.KillAllEnemies = v;
                    return Ok(cheat, v);
                }
            case "runtime_infinite_energy": {
                    var m = EnsureStatModifiers();
                    var v = enabled ?? !m.InfiniteEnergy;
                    m.InfiniteEnergy = v;
                    return Ok(cheat, v);
                }
            case "always_player_turn": {
                    var m = EnsureStatModifiers();
                    var v = enabled ?? !m.AlwaysPlayerTurn;
                    m.AlwaysPlayerTurn = v;
                    return Ok(cheat, v);
                }
            case "draw_to_hand_limit": {
                    var m = EnsureStatModifiers();
                    var v = enabled ?? !m.DrawToHandLimit;
                    m.DrawToHandLimit = v;
                    return Ok(cheat, v);
                }
            case "extra_draw": {
                    var m = EnsureStatModifiers();
                    if (value.HasValue) {
                        m.ExtraDrawEachTurn = true;
                        m.ExtraDrawEachTurnAmount = Math.Clamp((int)value.Value, 1, 20);
                        return new JsonObject {
                            ["ok"] = true,
                            ["cheat"] = cheat,
                            ["enabled"] = true,
                            ["value"] = m.ExtraDrawEachTurnAmount,
                        };
                    }
                    var v = enabled ?? !m.ExtraDrawEachTurn;
                    m.ExtraDrawEachTurn = v;
                    return new JsonObject {
                        ["ok"] = true,
                        ["cheat"] = cheat,
                        ["enabled"] = v,
                        ["value"] = m.ExtraDrawEachTurnAmount,
                    };
                }
            case "auto_ally": {
                    var m = EnsureStatModifiers();
                    var v = enabled ?? !m.AutoActFriendlyMonsters;
                    m.AutoActFriendlyMonsters = v;
                    return Ok(cheat, v);
                }
            case "negate_debuffs": {
                    var m = EnsureStatModifiers();
                    var v = enabled ?? !m.NegateDebuffs;
                    m.NegateDebuffs = v;
                    return Ok(cheat, v);
                }

            default:
                return Fail(
                    $"Unknown cheat '{cheat}'. Use patch toggles (freeze_enemies, infinite_hp, ...), " +
                    "multipliers (damage_multiplier, ...), or runtime toggles (god_mode, kill_all, ...).");
        }
    }

    public static JsonObject? ApplyStat(string stat, int value, bool? lockEnabled) {
        if (MpCheatSession.InMultiplayerRun)
            return Fail("Stat edits are not supported via MCP in multiplayer.");

        if (!RunContext.TryGetRunAndPlayer(out _, out var player))
            return Fail("No active run.");

        var m = EnsureStatModifiers();

        switch (stat) {
            case "gold":
                if (lockEnabled == true) {
                    m.LockGold = true;
                    m.LockedGoldValue = Math.Max(0, value);
                    return StatResult(stat, value, locked: true);
                }
                if (lockEnabled == false)
                    m.LockGold = false;
                player.Gold = Math.Max(0, value);
                return StatResult(stat, player.Gold, locked: m.LockGold);

            case "current_hp":
                if (lockEnabled == true) {
                    m.LockCurrentHp = true;
                    m.LockedCurrentHpValue = Math.Clamp(value, 1, 9999);
                    return StatResult(stat, m.LockedCurrentHpValue, locked: true);
                }
                if (lockEnabled == false)
                    m.LockCurrentHp = false;
                TaskHelper.RunSafely(Sts2ApiCompat.SetCurrentHpAsync(player.Creature, Math.Max(1, value)));
                return StatResult(stat, value, locked: m.LockCurrentHp);

            case "max_hp":
                if (lockEnabled == true) {
                    m.LockMaxHp = true;
                    m.LockedMaxHpValue = Math.Clamp(value, 1, 9999);
                    return StatResult(stat, m.LockedMaxHpValue, locked: true);
                }
                if (lockEnabled == false)
                    m.LockMaxHp = false;
                TaskHelper.RunSafely(Sts2ApiCompat.SetMaxHpAsync(player.Creature, Math.Max(1, value)));
                return StatResult(stat, value, locked: m.LockMaxHp);

            case "current_energy":
                if (lockEnabled == true) {
                    m.LockCurrentEnergy = true;
                    m.LockedCurrentEnergyValue = Math.Clamp(value, 0, 99);
                    return StatResult(stat, m.LockedCurrentEnergyValue, locked: true);
                }
                if (lockEnabled == false)
                    m.LockCurrentEnergy = false;
                if (player.PlayerCombatState != null)
                    player.PlayerCombatState.Energy = Math.Clamp(value, 0, 99);
                return StatResult(stat, value, locked: m.LockCurrentEnergy);

            case "max_energy":
                if (lockEnabled == true) {
                    m.LockMaxEnergy = true;
                    m.LockedMaxEnergyValue = Math.Clamp(value, 1, 99);
                    return StatResult(stat, m.LockedMaxEnergyValue, locked: true);
                }
                if (lockEnabled == false)
                    m.LockMaxEnergy = false;
                player.MaxEnergy = Math.Clamp(value, 1, 99);
                return StatResult(stat, player.MaxEnergy, locked: m.LockMaxEnergy);

            case "stars":
                if (lockEnabled == true) {
                    m.LockStars = true;
                    m.LockedStarsValue = Math.Max(0, value);
                    return StatResult(stat, m.LockedStarsValue, locked: true);
                }
                if (lockEnabled == false)
                    m.LockStars = false;
                if (player.PlayerCombatState != null)
                    player.PlayerCombatState.Stars = Math.Max(0, value);
                return StatResult(stat, value, locked: m.LockStars);

            case "orb_slots":
                if (lockEnabled == true) {
                    m.LockOrbSlots = true;
                    m.LockedOrbSlotsValue = Math.Clamp(value, 0, 10);
                    return StatResult(stat, m.LockedOrbSlotsValue, locked: true);
                }
                if (lockEnabled == false)
                    m.LockOrbSlots = false;
                player.BaseOrbSlotCount = Math.Clamp(value, 0, 10);
                return StatResult(stat, player.BaseOrbSlotCount, locked: m.LockOrbSlots);

            case "potion_slots": {
                    int current = player.MaxPotionCount;
                    int diff = value - current;
                    if (diff > 0)
                        player.AddToMaxPotionCount(diff);
                    else if (diff < 0) {
                        for (int i = current - 1; i >= current + diff; i--) {
                            var potion = player.GetPotionAtSlotIndex(i);
                            if (potion != null) player.DiscardPotionInternal(potion);
                        }
                        player.SubtractFromMaxPotionCount(-diff);
                    }
                    return StatResult(stat, player.MaxPotionCount, locked: false);
                }

            default:
                return Fail(
                    $"Unknown stat '{stat}'. Use gold, current_hp, max_hp, current_energy, max_energy, " +
                    "stars, orb_slots, or potion_slots.");
        }
    }

    private static bool IsRuntimeCheat(string cheat) => cheat is
        "god_mode" or "kill_all" or "runtime_infinite_energy" or "always_player_turn"
        or "draw_to_hand_limit" or "extra_draw" or "auto_ally" or "negate_debuffs";

    private static JsonObject ApplyMultiplier(string cheat, float? value, float current, Action<float> setter) {
        if (!value.HasValue)
            return new JsonObject { ["ok"] = true, ["cheat"] = cheat, ["value"] = current };
        if (value.Value < 0)
            return Fail("Multiplier value must be >= 0.");
        setter(value.Value);
        return new JsonObject { ["ok"] = true, ["cheat"] = cheat, ["value"] = value.Value };
    }

    private static JsonObject Ok(string cheat, bool enabled) => new() {
        ["ok"] = true,
        ["cheat"] = cheat,
        ["enabled"] = enabled,
    };

    private static JsonObject StatResult(string stat, int value, bool locked) => new() {
        ["ok"] = true,
        ["stat"] = stat,
        ["value"] = value,
        ["locked"] = locked,
    };
}
