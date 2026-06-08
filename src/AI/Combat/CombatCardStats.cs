using System;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

internal static class CombatCardStats {
    public static int ResolveDamage(JsonObject card) {
        var fromSnapshot = card["damage"]?.GetValue<int>();
        if (fromSnapshot is > 0)
            return fromSnapshot.Value;

        var id = card["id"]?.GetValue<string>();
        if (CardMechanicIndex.TryGet(id, out var profile) && profile.Damage is > 0)
            return profile.Damage.Value;

        return 0;
    }

    public static int ResolveHitCount(JsonObject card, int energySpent = 1) {
        var profile = ResolveProfile(card);
        return ResolveHitCount(profile, card, energySpent);
    }

    public static int ResolveHitCount(CardMechanicProfile profile, JsonObject? card, int energySpent = 1) {
        if (profile.AttackHitsScaleWithEnergy)
            return ResolveEnergyScaledHits(profile.Id, Math.Max(0, energySpent));

        if (profile.HitScaleMode != AttackHitScaleMode.None)
            return ResolveScaledHits(profile.HitScaleMode, profile.Id, energySpent, attacksPlayed: 0, skillsInHand: 0, orbCount: 0, statusCards: 0, unblockedDamage: 0);

        var fromSnapshot = card?["hitCount"]?.GetValue<int>();
        if (fromSnapshot is > 1)
            return fromSnapshot.Value;

        if (profile.AttackHitCount > 1)
            return profile.AttackHitCount;

        return Math.Max(1, fromSnapshot ?? 1);
    }

    public static int ResolveEffectiveHitCount(CombatHandCard card, CombatState state, int skillsInHand = 0) {
        if (card.Profile.AttackHitsScaleWithEnergy) {
            int energySpent = CombatCardCost.EffectiveCost(card, state);
            return ResolveEnergyScaledHits(card.Id, energySpent);
        }

        if (card.Profile.HitScaleMode != AttackHitScaleMode.None) {
            int energySpent = CombatCardCost.EffectiveCost(card, state);
            return ResolveScaledHits(
                card.Profile.HitScaleMode,
                card.Id,
                energySpent,
                state.AttacksPlayedThisTurn,
                skillsInHand,
                state.OrbCount,
                CombatCardPlayEffects.CountStatusCards(state),
                state.UnblockedDamageTakenThisTurn);
        }

        return Math.Max(1, card.HitCount);
    }

    public static int ResolveScaledHits(
        AttackHitScaleMode mode,
        string cardId,
        int energySpent,
        int attacksPlayed,
        int skillsInHand,
        int orbCount,
        int statusCards,
        int unblockedDamage) => mode switch {
        AttackHitScaleMode.Energy => ResolveEnergyScaledHits(cardId, energySpent),
        AttackHitScaleMode.AttacksPlayedThisTurn => Math.Max(0, attacksPlayed),
        AttackHitScaleMode.SkillsInHand => Math.Max(0, skillsInHand),
        AttackHitScaleMode.OrbCount => Math.Max(0, orbCount),
        AttackHitScaleMode.StatusCardsOwned => Math.Max(0, statusCards),
        AttackHitScaleMode.UnblockedDamageTakenPlusOne => Math.Max(1, 1 + unblockedDamage),
        _ => 1,
    };

    public static int ResolveEnergyCost(JsonObject card, int availableEnergy) {
        var profile = ResolveProfile(card);
        if (profile.CostsEnergyX || card["costsX"]?.GetValue<bool>() == true)
            return Math.Max(0, availableEnergy);
        return card["cost"]?.GetValue<int>() ?? 99;
    }

    static int ResolveEnergyScaledHits(string id, int energySpent) {
        if (string.Equals(id, "HEAVENLY_DRILL", StringComparison.OrdinalIgnoreCase) && energySpent >= 4)
            return energySpent * 2;
        return energySpent;
    }

    public static int ResolveBlock(JsonObject card) {
        var fromSnapshot = card["block"]?.GetValue<int>();
        if (fromSnapshot is > 0)
            return fromSnapshot.Value;

        var id = card["id"]?.GetValue<string>();
        if (CardMechanicIndex.TryGet(id, out var profile) && profile.Block is > 0)
            return profile.Block.Value;

        return 0;
    }

    public static CardMechanicProfile ResolveProfile(JsonObject card) =>
        CardMechanicIndex.TryGet(card["id"]?.GetValue<string>(), out var profile)
            ? profile
            : CardMechanicIndex.InferFromSnapshot(card);

    public static int CountHandAttacks(JsonArray? hand) {
        if (hand == null) return 0;
        var count = 0;
        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            if (IsAttackCard(card))
                count++;
        }
        return count;
    }

    public static int EstimateFollowupAttackDamage(JsonArray? hand, int energy) {
        if (hand == null || energy <= 0) return 0;

        var attacks = new System.Collections.Generic.List<(int Cost, int Damage)>();
        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            if (!IsAttackCard(card)) continue;
            var cost = ResolveEnergyCost(card, energy);
            if (cost > energy) continue;
            attacks.Add((cost, ResolveDamage(card) * ResolveHitCount(card, cost)));
        }

        attacks.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        var remaining = energy;
        var total = 0;
        foreach (var (cost, damage) in attacks) {
            if (cost > remaining) continue;
            remaining -= cost;
            total += damage;
        }

        return total;
    }

    public static bool IsAttackCard(JsonObject card) {
        var cardType = card["cardType"]?.GetValue<string>() ?? "";
        return cardType.Contains("Attack", StringComparison.OrdinalIgnoreCase)
            || ResolveDamage(card) > 0;
    }

    public static bool IsSkillCard(JsonObject card) {
        var cardType = card["cardType"]?.GetValue<string>() ?? "";
        return cardType.Contains("Skill", StringComparison.OrdinalIgnoreCase);
    }
}
