using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace DevMode.Patches;

/// <summary>Infinite HP — prevent player HP loss.</summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
public static class InfiniteHpPatch {
    public static bool Prefix(Creature __instance, ref DamageResult __result, decimal amount, ValueProp props) {
        if (!DevModeState.CheatsInRun || !DevModeState.PlayerCheats.InfiniteHp) return true;
        if (__instance.Player == null) return true;
        __result = new DamageResult(__instance, props) {
            UnblockedDamage = 0,
            WasTargetKilled = false,
            OverkillDamage = 0
        };
        return false;
    }
}

/// <summary>Infinite Block — refill player block after loss.</summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.LoseBlockInternal))]
public static class InfiniteBlockPatch {
    public static void Postfix(Creature __instance) {
        if (!DevModeState.CheatsInRun || !DevModeState.PlayerCheats.InfiniteBlock) return;
        if (__instance.Player == null) return;
        __instance.GainBlockInternal(999 - __instance.Block);
    }
}

/// <summary>Infinite Block — prevent block consumption during damage calculation.</summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
[HarmonyPriority(Priority.High)]
public static class InfiniteBlockDamagePatch {
    public static bool Prefix(Creature __instance, decimal amount, ValueProp props, ref decimal __result) {
        if (!DevModeState.CheatsInRun || !DevModeState.PlayerCheats.InfiniteBlock) return true;
        if (__instance.Player == null) return true;
        // Report that all damage was blocked, but don't actually reduce block
        __result = props.HasFlag(ValueProp.Unblockable) ? 0m : Math.Min(__instance.Block, amount);
        return false;
    }
}

/// <summary>Infinite Block — prevent block clear at turn start.
/// Hook.ShouldClearBlock is called by Creature.ClearBlock() to decide
/// whether to set Block = 0. We make it return false for the player.</summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldClearBlock))]
public static class InfiniteBlockClearPatch {
    public static bool Prefix(Creature creature, ref bool __result, ref AbstractModel? preventer) {
        if (!DevModeState.CheatsInRun || !DevModeState.PlayerCheats.InfiniteBlock) return true;
        if (creature.Player == null) return true;
        __result = false;
        preventer = null;
        return false;
    }
}

/// <summary>Infinite Energy — refill energy after spending.</summary>
[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.LoseEnergy))]
public static class InfiniteEnergyPatch {
    public static void Postfix(PlayerCombatState __instance) {
        if (!DevModeState.CheatsInRun || !DevModeState.PlayerCheats.InfiniteEnergy) return;
        __instance.Energy = 999;
    }
}

/// <summary>Infinite Stars — refill stars after spending.</summary>
[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.LoseStars))]
public static class InfiniteStarsPatch {
    public static void Postfix(PlayerCombatState __instance) {
        if (!DevModeState.CheatsInRun || !DevModeState.PlayerCheats.InfiniteStars) return;
        __instance.Stars = 999;
    }
}

/// <summary>Always reward potion — force potion drop from combat.</summary>
[HarmonyPatch(typeof(PotionRewardOdds), nameof(PotionRewardOdds.Roll))]
public static class AlwaysPotionRewardPatch {
    public static void Postfix(ref bool __result) {
        if (!DevModeState.CheatsInRun || !DevModeState.PlayerCheats.AlwaysRewardPotion) return;
        __result = true;
    }
}

/// <summary>Always upgrade card rewards.</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Factories.CardFactory), "RollForUpgrade",
    [typeof(Player), typeof(MegaCrit.Sts2.Core.Models.CardModel), typeof(decimal), typeof(MegaCrit.Sts2.Core.Random.Rng)])]
public static class AlwaysUpgradeRewardPatch {
    public static void Prefix(ref decimal baseChance) {
        if (!DevModeState.CheatsInRun || !DevModeState.PlayerCheats.AlwaysUpgradeCardReward) return;
        baseChance = 999m;
    }
}

/// <summary>Max card reward rarity — all card rewards are Rare.</summary>
[HarmonyPatch(typeof(CardRarityOdds), nameof(CardRarityOdds.Roll))]
public static class MaxCardRarityPatch {
    public static void Postfix(ref CardRarity __result) {
        if (!DevModeState.CheatsInRun || !DevModeState.PlayerCheats.MaxCardRewardRarity) return;
        __result = CardRarity.Rare;
    }
}

/// <summary>Defense multiplier — multiply block gained by player.</summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.GainBlockInternal))]
public static class DefenseMultiplierPatch {
    public static void Prefix(Creature __instance, ref decimal amount) {
        if (!DevModeState.CheatsInRun || DevModeState.PlayerCheats.DefenseMultiplier == 1.0f) return;
        if (__instance.Player == null) return;
        amount = Math.Round(amount * (decimal)DevModeState.PlayerCheats.DefenseMultiplier);
    }
}

/// <summary>Gold multiplier — multiply gold gained.</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Commands.PlayerCmd), nameof(MegaCrit.Sts2.Core.Commands.PlayerCmd.GainGold))]
public static class GoldMultiplierPatch {
    public static void Prefix(ref decimal amount) {
        if (!DevModeState.CheatsInRun || DevModeState.GameplayModifiers.GoldMultiplier == 1.0f) return;
        amount = Math.Round(amount * (decimal)DevModeState.GameplayModifiers.GoldMultiplier);
    }
}

/// <summary>Free shop — bypass gold cost for merchant purchases.</summary>
[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper))]
public static class FreeShopPatch {
    public static void Prefix(ref bool ignoreCost) {
        if (!DevModeState.CheatsInRun || !DevModeState.GameplayModifiers.FreeShop) return;
        ignoreCost = true;
    }
}

/// <summary>Freeze enemies — skip enemy turns entirely.</summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.TakeTurn))]
public static class FreezeEnemiesPatch {
    public static bool Prefix(Creature __instance, ref Task __result) {
        if (!DevModeState.CheatsInRun || !DevModeState.EnemyCheats.FreezeEnemies) return true;
        if (__instance.Player != null) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>One-hit kill — enemies take massive damage.</summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
[HarmonyPriority(Priority.High)]
public static class OneHitKillPatch {
    public static void Prefix(Creature __instance, ref decimal amount) {
        if (!DevModeState.CheatsInRun || !DevModeState.EnemyCheats.OneHitKill) return;
        if (__instance.Player != null) return;
        amount = 999999m;
    }
}

/// <summary>Damage multiplier — multiply damage dealt to enemies.</summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
public static class DamageMultiplierPatch {
    public static void Prefix(Creature __instance, ref decimal amount) {
        if (!DevModeState.CheatsInRun || DevModeState.EnemyCheats.DamageMultiplier == 1.0f) return;
        if (__instance.Player != null) return;
        amount = Math.Round(amount * (decimal)DevModeState.EnemyCheats.DamageMultiplier);
    }
}

/// <summary>Unknown map points always resolve to Treasure.</summary>
[HarmonyPatch(typeof(UnknownMapPointOdds), nameof(UnknownMapPointOdds.Roll))]
public static class UnknownMapTreasurePatch {
    public static void Postfix(ref RoomType __result) {
        if (!DevModeState.CheatsInRun || !DevModeState.MapCheats.UnknownMapAlwaysTreasure) return;
        __result = RoomType.Treasure;
    }
}

/// <summary>
/// Allow free map travel when map rewrite is enabled.
/// This unlocks all next-row nodes in map UI travelability checks.
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldAllowFreeTravel))]
public static class MapFreeTravelPatch {
    public static bool Prefix(ref bool __result) {
        if (!DevModeState.CheatsInRun || !DevModeState.MapCheats.FreeTravelFromDevRoomMap) return true;
        __result = true;
        return false;
    }
}

/// <summary>
/// When map rewrite is enabled, force all currently untravelable map points
/// into travelable state after vanilla travelability recalculation.
/// </summary>
[HarmonyPatch(typeof(NMapScreen), "RecalculateTravelability")]
public static class MapTravelabilityRewritePatch {
    private static readonly AccessTools.FieldRef<NMapScreen, Dictionary<MapCoord, NMapPoint>> _mapPointDictionaryRef =
        AccessTools.FieldRefAccess<NMapScreen, Dictionary<MapCoord, NMapPoint>>("_mapPointDictionary");

    public static void Postfix(NMapScreen __instance) {
        if (!DevModeState.CheatsInRun || !DevModeState.MapCheats.FreeTravelFromDevRoomMap) return;

        Dictionary<MapCoord, NMapPoint>? dict;
        try {
            dict = _mapPointDictionaryRef(__instance);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[DevMode] MapTravelabilityRewritePatch: failed to access map points: {ex.Message}");
            return;
        }

        if (dict == null || dict.Count == 0) return;

        foreach (NMapPoint point in dict.Values) {
            if (point.State == MapPointState.Untravelable) {
                point.State = MapPointState.Travelable;
            }
        }
    }
}

/// <summary>Fix NPotionContainer.GrowPotionHolders to also handle shrinking.
/// The vanilla method only adds holders; when DevMode reduces potion slots the
/// UI holders were never removed, causing index-out-of-range crashes.</summary>
[HarmonyPatch(typeof(NPotionContainer), "GrowPotionHolders")]
public static class PotionContainerShrinkPatch {
    public static void Postfix(NPotionContainer __instance, int newMaxPotionSlots) {
        if (!DevModeState.InDevRun) return;

        var holdersField = AccessTools.Field(typeof(NPotionContainer), "_holders");
        if (holdersField == null) return;
        var holders = holdersField.GetValue(__instance) as List<NPotionHolder>;
        if (holders == null || holders.Count <= newMaxPotionSlots) return;

        // Remove excess holders from the end
        for (int i = holders.Count - 1; i >= newMaxPotionSlots; i--) {
            var holder = holders[i];
            holders.RemoveAt(i);
            holder.QueueFree();
        }
    }
}
