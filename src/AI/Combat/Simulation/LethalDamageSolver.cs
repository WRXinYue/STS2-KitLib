using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat;

namespace KitLib.AI.Combat.Simulation;

public static class LethalDamageSolver {
    public const int AoeWipeSignal = int.MaxValue / 4;

    public static int MaxSingleTargetDamage(CombatState state, int targetIndex) {
        var target = FindEnemy(state, targetIndex);
        int vulnerable = target?.Vulnerable ?? 0;

        var attacks = CollectAttacks(state, vulnerable);
        if (attacks.Any(a => a.IsAoe && AoeDamageEstimator.EstimateAoeKills(state, RawDamage(state, a.HandIndex)) > 0))
            return AoeWipeSignal;

        return SearchMaxDamage(attacks, state.Energy);
    }

    public static int MaxSingleTargetDamage(JsonArray hand, int energy, int targetIndex, JsonArray? enemies) {
        int vulnerable = 0;
        if (enemies != null && targetIndex >= 0 && targetIndex < enemies.Count)
            vulnerable = CombatPowerReader.GetVulnerable(enemies[targetIndex]?.AsObject());

        var attacks = CollectAttacks(hand, energy, vulnerable);
        return SearchMaxDamage(attacks, energy);
    }

    static List<AttackOption> CollectAttacks(CombatState state, int vulnerable) {
        var attacks = new List<AttackOption>();
        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!card.CanPlay || !card.IsAttack || card.Damage <= 0) continue;
            var cost = CombatCardCost.EffectiveCost(card, state);
            if (cost > state.Energy) continue;

            attacks.Add(new AttackOption(
                i,
                cost,
                CombatDamageCalc.OutgoingDamage(card, state, vulnerable),
                card.IsAoe));
        }

        return attacks;
    }

    static int RawDamage(CombatState state, int handIndex) {
        if (handIndex < 0 || handIndex >= state.Hand.Count) return 0;
        return state.Hand[handIndex].Damage;
    }

    static List<AttackOption> CollectAttacks(JsonArray hand, int maxEnergy, int vulnerable) {
        var attacks = new List<AttackOption>();
        for (int i = 0; i < hand.Count; i++) {
            if (hand[i] is not JsonObject card) continue;
            if (!CombatCardStats.IsAttackCard(card)) continue;

            var cost = CombatCardStats.ResolveEnergyCost(card, maxEnergy);
            if (cost > maxEnergy) continue;

            var damage = CombatCardStats.ResolveDamage(card) * CombatCardStats.ResolveHitCount(card, cost);
            if (damage <= 0) continue;

            var targetType = card["targetType"]?.GetValue<string>() ?? "";
            attacks.Add(new AttackOption(
                i,
                cost,
                CombatDamageCalc.OutgoingDamage(damage, [], vulnerable),
                CombatTargetTypes.IsAllEnemies(targetType)));
        }

        return attacks;
    }

    static int ScaleDamage(int damage, int vulnerable) {
        if (vulnerable <= 0) return damage;
        return (int)Math.Round(damage * 1.5f);
    }

    static int SearchMaxDamage(IReadOnlyList<AttackOption> attacks, int energy) {
        int best = 0;
        Search(attacks, 0, energy, 0, ref best);
        return best;
    }

    static void Search(
        IReadOnlyList<AttackOption> attacks,
        int start,
        int remainingEnergy,
        int totalDamage,
        ref int best) {
        best = Math.Max(best, totalDamage);
        for (int i = start; i < attacks.Count; i++) {
            var attack = attacks[i];
            if (attack.Cost > remainingEnergy) continue;
            Search(attacks, i + 1, remainingEnergy - attack.Cost, totalDamage + attack.Damage, ref best);
        }
    }

    static CombatEnemy? FindEnemy(CombatState state, int targetIndex) {
        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive) continue;
            if (enemy.Index == targetIndex)
                return enemy;
        }

        return null;
    }

    readonly record struct AttackOption(int HandIndex, int Cost, int Damage, bool IsAoe);
}
