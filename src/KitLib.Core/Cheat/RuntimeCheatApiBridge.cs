using System;
using KitLib.Abstractions.Host;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;

namespace KitLib.Cheat;

/// <summary>Wires <see cref="KitLibRuntimeCheatApi"/> for patch/runtime toggles and run stats.</summary>
internal static class RuntimeCheatApiBridge {
    public static void Wire() {
        KitLibRuntimeCheatApi.IsAvailable = () => true;
        KitLibRuntimeCheatApi.TrySetCheat = TrySetCheat;
        KitLibRuntimeCheatApi.TrySetStat = TrySetStat;
    }

    static KitLibCheatOpResult TrySetCheat(KitLibSetCheatRequest request) {
        var cheat = (request.Cheat ?? "").Trim().ToLowerInvariant().Replace('-', '_');
        if (string.IsNullOrEmpty(cheat))
            return KitLibCheatOpResult.Fail("Missing cheat name.");

        if (MpCheatSession.InMultiplayerRun && IsRuntimeCheat(cheat))
            return KitLibCheatOpResult.Fail("Runtime cheats are not supported via API in multiplayer.");

        var enabled = request.Enabled;
        var value = request.Value;

        switch (cheat) {
            case "infinite_hp":
            case "godmode": {
                    var v = enabled ?? !KitLibState.PlayerCheats.InfiniteHp;
                    KitLibState.PlayerCheats.InfiniteHp = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "infinite_block": {
                    var v = enabled ?? !KitLibState.PlayerCheats.InfiniteBlock;
                    KitLibState.PlayerCheats.InfiniteBlock = v;
                    if (!MpCheatSession.InMultiplayerRun && v && RunContext.TryGetRunAndPlayer(out _, out var bp) && bp != null) {
                        var c = bp.Creature;
                        if (c.Block < 999) c.GainBlockInternal(999 - c.Block);
                    }
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "infinite_energy": {
                    var v = enabled ?? !KitLibState.PlayerCheats.InfiniteEnergy;
                    KitLibState.PlayerCheats.InfiniteEnergy = v;
                    if (v) PlayerCheatEffects.ApplyImmediateIfEnabled();
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "infinite_stars": {
                    var v = enabled ?? !KitLibState.PlayerCheats.InfiniteStars;
                    KitLibState.PlayerCheats.InfiniteStars = v;
                    if (v) PlayerCheatEffects.ApplyImmediateIfEnabled();
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "freeze_enemies": {
                    var v = enabled ?? !KitLibState.EnemyCheats.FreezeEnemies;
                    KitLibState.EnemyCheats.FreezeEnemies = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "free_shop": {
                    var v = enabled ?? !KitLibState.GameplayModifiers.FreeShop;
                    KitLibState.GameplayModifiers.FreeShop = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "always_potion": {
                    var v = enabled ?? !KitLibState.PlayerCheats.AlwaysRewardPotion;
                    KitLibState.PlayerCheats.AlwaysRewardPotion = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "always_upgrade": {
                    var v = enabled ?? !KitLibState.PlayerCheats.AlwaysUpgradeCardReward;
                    KitLibState.PlayerCheats.AlwaysUpgradeCardReward = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "max_rarity": {
                    var v = enabled ?? !KitLibState.PlayerCheats.MaxCardRewardRarity;
                    KitLibState.PlayerCheats.MaxCardRewardRarity = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "unknown_treasure": {
                    var v = enabled ?? !KitLibState.MapCheats.UnknownMapAlwaysTreasure;
                    KitLibState.MapCheats.UnknownMapAlwaysTreasure = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "max_score": {
                    var v = enabled ?? !KitLibState.GameplayModifiers.MaxScore;
                    KitLibState.GameplayModifiers.MaxScore = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }

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

            case "god_mode": {
                    var m = CheatRunState.Ensure();
                    var v = enabled ?? !m.GodMode;
                    m.GodMode = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "kill_all": {
                    var v = enabled ?? !KillAllEnemiesCheat.IsEnabled;
                    KillAllEnemiesCheat.SetEnabled(v);
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "runtime_infinite_energy": {
                    var m = CheatRunState.Ensure();
                    var v = enabled ?? !m.InfiniteEnergy;
                    m.InfiniteEnergy = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "always_player_turn": {
                    var m = CheatRunState.Ensure();
                    var v = enabled ?? !m.AlwaysPlayerTurn;
                    m.AlwaysPlayerTurn = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "draw_to_hand_limit": {
                    var m = CheatRunState.Ensure();
                    var v = enabled ?? !m.DrawToHandLimit;
                    m.DrawToHandLimit = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "extra_draw": {
                    var m = CheatRunState.Ensure();
                    if (value.HasValue) {
                        m.ExtraDrawEachTurn = true;
                        m.ExtraDrawEachTurnAmount = Math.Clamp((int)value.Value, 1, 20);
                        return KitLibCheatOpResult.Success(cheat, true, m.ExtraDrawEachTurnAmount);
                    }
                    var v = enabled ?? !m.ExtraDrawEachTurn;
                    m.ExtraDrawEachTurn = v;
                    return KitLibCheatOpResult.Success(cheat, v, m.ExtraDrawEachTurnAmount);
                }
            case "auto_ally": {
                    var m = CheatRunState.Ensure();
                    var v = enabled ?? !m.AutoActFriendlyMonsters;
                    m.AutoActFriendlyMonsters = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }
            case "negate_debuffs": {
                    var m = CheatRunState.Ensure();
                    var v = enabled ?? !m.NegateDebuffs;
                    m.NegateDebuffs = v;
                    return KitLibCheatOpResult.Success(cheat, v);
                }

            default:
                return KitLibCheatOpResult.Fail(
                    $"Unknown cheat '{cheat}'. Use patch toggles (freeze_enemies, infinite_hp, ...), " +
                    "multipliers (damage_multiplier, ...), or runtime toggles (god_mode, kill_all, ...).");
        }
    }

    static KitLibStatOpResult TrySetStat(KitLibSetStatRequest request) {
        if (MpCheatSession.InMultiplayerRun)
            return KitLibStatOpResult.Fail("Stat edits are not supported via API in multiplayer.");

        if (!RunContext.TryGetRunAndPlayer(out _, out var player) || player == null)
            return KitLibStatOpResult.Fail("No active run.");

        var m = CheatRunState.Ensure();
        var lockEnabled = request.LockEnabled;
        var value = request.Value;
        var statKey = StatKey(request.Stat);

        switch (request.Stat) {
            case KitLibRunStat.Gold:
                if (lockEnabled == true) {
                    m.LockGold = true;
                    m.LockedGoldValue = Math.Max(0, value);
                    player.Gold = m.LockedGoldValue;
                    return KitLibStatOpResult.Success(statKey, player.Gold, locked: true);
                }
                if (lockEnabled == false)
                    m.LockGold = false;
                player.Gold = Math.Max(0, value);
                return KitLibStatOpResult.Success(statKey, player.Gold, locked: m.LockGold);

            case KitLibRunStat.CurrentHp:
                if (lockEnabled == true) {
                    m.LockCurrentHp = true;
                    m.LockedCurrentHpValue = Math.Clamp(value, 1, 9999);
                    TaskHelper.RunSafely(CreatureCmd.SetCurrentHp(player.Creature, m.LockedCurrentHpValue));
                    return KitLibStatOpResult.Success(statKey, player.Creature.CurrentHp, locked: true);
                }
                if (lockEnabled == false)
                    m.LockCurrentHp = false;
                TaskHelper.RunSafely(CreatureCmd.SetCurrentHp(player.Creature, Math.Max(1, value)));
                return KitLibStatOpResult.Success(statKey, value, locked: m.LockCurrentHp);

            case KitLibRunStat.MaxHp:
                if (lockEnabled == true) {
                    m.LockMaxHp = true;
                    m.LockedMaxHpValue = Math.Clamp(value, 1, 9999);
                    TaskHelper.RunSafely(CreatureCmd.SetMaxHp(player.Creature, m.LockedMaxHpValue));
                    return KitLibStatOpResult.Success(statKey, player.Creature.MaxHp, locked: true);
                }
                if (lockEnabled == false)
                    m.LockMaxHp = false;
                TaskHelper.RunSafely(CreatureCmd.SetMaxHp(player.Creature, Math.Max(1, value)));
                return KitLibStatOpResult.Success(statKey, value, locked: m.LockMaxHp);

            case KitLibRunStat.CurrentEnergy:
                if (lockEnabled == true) {
                    m.LockCurrentEnergy = true;
                    m.LockedCurrentEnergyValue = Math.Clamp(value, 0, 99);
                    if (player.PlayerCombatState != null)
                        player.PlayerCombatState.Energy = m.LockedCurrentEnergyValue;
                    return KitLibStatOpResult.Success(
                        statKey, player.PlayerCombatState?.Energy ?? m.LockedCurrentEnergyValue, locked: true);
                }
                if (lockEnabled == false)
                    m.LockCurrentEnergy = false;
                if (player.PlayerCombatState != null)
                    player.PlayerCombatState.Energy = Math.Clamp(value, 0, 99);
                return KitLibStatOpResult.Success(statKey, value, locked: m.LockCurrentEnergy);

            case KitLibRunStat.MaxEnergy:
                if (lockEnabled == true) {
                    m.LockMaxEnergy = true;
                    m.LockedMaxEnergyValue = Math.Clamp(value, 1, 99);
                    player.MaxEnergy = m.LockedMaxEnergyValue;
                    return KitLibStatOpResult.Success(statKey, player.MaxEnergy, locked: true);
                }
                if (lockEnabled == false)
                    m.LockMaxEnergy = false;
                player.MaxEnergy = Math.Clamp(value, 1, 99);
                return KitLibStatOpResult.Success(statKey, player.MaxEnergy, locked: m.LockMaxEnergy);

            case KitLibRunStat.Stars:
                if (lockEnabled == true) {
                    m.LockStars = true;
                    m.LockedStarsValue = Math.Max(0, value);
                    if (player.PlayerCombatState != null)
                        player.PlayerCombatState.Stars = m.LockedStarsValue;
                    return KitLibStatOpResult.Success(
                        statKey, player.PlayerCombatState?.Stars ?? m.LockedStarsValue, locked: true);
                }
                if (lockEnabled == false)
                    m.LockStars = false;
                if (player.PlayerCombatState != null)
                    player.PlayerCombatState.Stars = Math.Max(0, value);
                return KitLibStatOpResult.Success(statKey, value, locked: m.LockStars);

            case KitLibRunStat.OrbSlots:
                if (lockEnabled == true) {
                    m.LockOrbSlots = true;
                    m.LockedOrbSlotsValue = Math.Clamp(value, 0, 10);
                    return KitLibStatOpResult.Success(statKey, m.LockedOrbSlotsValue, locked: true);
                }
                if (lockEnabled == false)
                    m.LockOrbSlots = false;
                player.BaseOrbSlotCount = Math.Clamp(value, 0, 10);
                return KitLibStatOpResult.Success(statKey, player.BaseOrbSlotCount, locked: m.LockOrbSlots);

            case KitLibRunStat.PotionSlots: {
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
                    return KitLibStatOpResult.Success(statKey, player.MaxPotionCount, locked: false);
                }

            default:
                return KitLibStatOpResult.Fail($"Unknown stat '{request.Stat}'.");
        }
    }

    static string StatKey(KitLibRunStat stat) => stat switch {
        KitLibRunStat.Gold => "gold",
        KitLibRunStat.CurrentHp => "current_hp",
        KitLibRunStat.MaxHp => "max_hp",
        KitLibRunStat.CurrentEnergy => "current_energy",
        KitLibRunStat.MaxEnergy => "max_energy",
        KitLibRunStat.Stars => "stars",
        KitLibRunStat.OrbSlots => "orb_slots",
        KitLibRunStat.PotionSlots => "potion_slots",
        _ => stat.ToString(),
    };

    static bool IsRuntimeCheat(string cheat) => cheat is
        "god_mode" or "kill_all" or "runtime_infinite_energy" or "always_player_turn"
        or "draw_to_hand_limit" or "extra_draw" or "auto_ally" or "negate_debuffs";

    static KitLibCheatOpResult ApplyMultiplier(string cheat, float? value, float current, Action<float> setter) {
        if (!value.HasValue)
            return KitLibCheatOpResult.Success(cheat, value: current);
        if (value.Value < 0)
            return KitLibCheatOpResult.Fail("Multiplier value must be >= 0.");
        setter(value.Value);
        return KitLibCheatOpResult.Success(cheat, value: value.Value);
    }
}
