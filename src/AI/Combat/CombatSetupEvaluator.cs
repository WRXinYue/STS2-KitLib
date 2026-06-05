using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat.Simulation;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat;

/// <summary>Dynamic setup-vs-attack comparison from live snapshot metrics.</summary>
internal static class CombatSetupEvaluator {
    public static int ComputeVulnerableDeferValue(
        JsonObject snapshot,
        JsonArray? hand,
        int energy,
        JsonObject? targetEnemy,
        int vulnStacks,
        int vulnCost,
        int vulnCardIndex = -1,
        JsonObject? vulnCard = null) {
        if (hand == null || vulnStacks <= 0 || vulnCost > energy)
            return 0;
        if (CombatPowerReader.GetVulnerable(targetEnemy) > 0)
            return 0;
        if (targetEnemy != null && EnemyTargetPriority.IsMinion(targetEnemy)
            && EnemyTargetPriority.HasAliveNonMinion(snapshot["combat"]?["enemies"]?.AsArray()))
            return 0;
        if (vulnCardIndex < 0)
            return 0;

        var state = CombatState.FromSnapshot(snapshot);
        if (vulnCardIndex >= state.Hand.Count)
            return 0;

        var enemies = snapshot["combat"]?["enemies"]?.AsArray();
        int enemyIndex = ResolveCombatIndex(enemies, targetEnemy);
        if (enemyIndex < 0)
            return 0;

        return ComputeVulnerableSetupValue(state, vulnCardIndex, enemyIndex);
    }

    public static int ComputeVulnerableSetupValue(CombatState state, int handIndex, int enemyIndex) {
        if (handIndex < 0 || handIndex >= state.Hand.Count)
            return 0;

        var card = state.Hand[handIndex];
        if (!card.CanPlay || card.Cost > state.Energy)
            return 0;
        if (!AppliesVulnerable(card))
            return 0;

        var target = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == enemyIndex);
        if (target == null)
            return 0;
        if (target.Vulnerable > 0)
            return 0;

        if (target.IsMinion && state.Enemies.Any(e => e.IsAlive && !e.IsMinion))
            return 0;

        return Math.Max(0, ComputeVulnerableSetupSimDelta(state, handIndex, enemyIndex));
    }

    /// <summary>Terminal score delta: greedy attacks after vuln first vs skipping the vuln card.</summary>
    static int ComputeVulnerableSetupSimDelta(CombatState state, int handIndex, int enemyIndex) {
        int withoutVuln = EvaluateGreedyLineTerminal(state, handIndex);
        var afterVuln = CombatSimulator.Apply(
            state,
            new SimCombatAction(SimActionKind.PlayCard, handIndex, enemyIndex));
        int withVuln = EvaluateGreedyLineTerminal(afterVuln);
        return withVuln - withoutVuln;
    }

    static int EvaluateGreedyLineTerminal(CombatState state, int excludeHandIndex = -1) {
        var midTurn = SimulateGreedyAttacks(state, excludeHandIndex);
        var afterTurn = CombatTurnResolver.ResolveEndTurn(midTurn);
        return CombatEvaluator.EvaluateTerminal(afterTurn);
    }

    public static int ComputeBestVulnerableDeferValue(
        JsonObject snapshot,
        JsonArray? hand,
        int energy,
        JsonObject? targetEnemy) {
        if (hand == null) return 0;

        var best = 0;
        for (var i = 0; i < hand.Count; i++) {
            var card = hand[i]?.AsObject();
            if (card == null) continue;
            if (card["canPlay"]?.GetValue<bool>() == false) continue;

            var profile = CombatCardStats.ResolveProfile(card);
            if (!AppliesVulnerable(profile)) continue;

            var stacks = Math.Max(profile.AppliedVulnerable, 1);
            var cost = card["cost"]?.GetValue<int>() ?? 99;
            if (cost > energy) continue;

            var value = ComputeVulnerableDeferValue(
                snapshot, hand, energy, targetEnemy, stacks, cost, i, card);
            if (value > best)
                best = value;
        }

        return best;
    }

    public static int ComputeVulnerableDeferOpportunityCost(
        JsonObject snapshot,
        JsonArray? hand,
        int energy,
        JsonObject? targetEnemy,
        int attackDamage) {
        var deferValue = ComputeBestVulnerableDeferValue(snapshot, hand, energy, targetEnemy);
        if (deferValue <= 0)
            return 0;

        return Math.Max(0, deferValue - attackDamage);
    }

    public static int ComputeSetupDebt(CombatState state) {
        if (!state.Enemies.Any(e => e.IsAlive && e.Vulnerable <= 0))
            return 0;

        var hasVulnPlay = state.Hand.Any(c =>
            c.CanPlay && c.Cost <= state.Energy && AppliesVulnerable(c));
        if (!hasVulnPlay)
            return 0;

        var hasOtherAttack = state.Hand.Any(c =>
            c.CanPlay && c.Cost <= state.Energy && c.IsAttack && c.Damage > 0 && !AppliesVulnerable(c));
        if (!hasOtherAttack)
            return 0;

        int debt = 0;
        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!card.CanPlay || card.Cost > state.Energy) continue;
            if (!AppliesVulnerable(card)) continue;

            bool worthOpening = false;
            foreach (var enemy in state.Enemies.Where(e => e.IsAlive && e.Vulnerable <= 0)) {
                if (ComputeVulnerableSetupValue(state, i, enemy.Index) > 0) {
                    worthOpening = true;
                    break;
                }
            }

            if (!worthOpening)
                continue;

            debt += 12 + Math.Max(card.Profile.AppliedVulnerable, 1) * 5;
        }

        return debt;
    }

    /// <summary>Primary focus for single-target attacks this turn (matches beam target ordering).</summary>
    public static int PrimaryAttackTargetIndex(CombatState state) =>
        state.Enemies
            .Where(e => ThreatModel.IsViableAttackTarget(state, e))
            .OrderByDescending(e => e.IsMinion ? 0 : 1)
            .ThenBy(e => e.EffectiveHp)
            .ThenByDescending(e => e.IntentDamage)
            .ThenByDescending(e => ThreatModel.NextTurnAttackOn(e))
            .Select(e => e.Index)
            .FirstOrDefault();

    public static int ComputeWastedVulnerablePenalty(CombatState state) {
        if (state.AliveEnemyCount < 2)
            return 0;

        var focus = PrimaryAttackTargetIndex(state);
        if (focus < 0)
            return 0;

        var focusDamage = EstimateGreedyAttackDamageOn(state, focus);
        if (focusDamage <= 0)
            return 0;

        int penalty = 0;
        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive || enemy.Vulnerable <= 0 || enemy.Index == focus)
                continue;
            penalty += enemy.Vulnerable * 10 + Math.Min(30, focusDamage / 2);
        }

        return penalty;
    }

    public static int EstimateGreedyAttackDamageOn(CombatState state, int enemyIndex) {
        var target = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == enemyIndex);
        if (target == null)
            return 0;

        int energy = state.Energy;
        int total = 0;
        foreach (var card in state.Hand.OrderByDescending(c => c.Damage)) {
            if (!CombatCardCost.CanAfford(card, state))
                continue;
            if (!card.IsAttack || card.Damage <= 0)
                continue;

            int cost = CombatCardCost.EffectiveCost(card, state.Modifiers);
            if (cost > energy)
                continue;

            energy -= cost;
            total += CombatDamageCalc.OutgoingDamage(card, state, target.Vulnerable);
        }

        return Math.Max(0, total);
    }

    static int ResolveCombatIndex(JsonArray? enemies, JsonObject? targetEnemy) {
        if (enemies == null || targetEnemy == null)
            return -1;

        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i] is not JsonObject enemy)
                continue;
            if (ReferenceEquals(enemy, targetEnemy))
                return EnemyIndexResolver.CombatIndex(enemy, i);
        }

        return targetEnemy["index"]?.GetValue<int>() ?? -1;
    }

    static CombatState SimulateGreedyAttacks(CombatState state, int excludeHandIndex = -1) {
        var s = state;
        string? excludeId = excludeHandIndex >= 0 && excludeHandIndex < state.Hand.Count
            ? state.Hand[excludeHandIndex].Id
            : null;

        bool played = true;
        while (played) {
            played = false;
            int bestHand = -1;
            int bestEnemy = -1;
            int bestScore = int.MinValue;

            for (int i = 0; i < s.Hand.Count; i++) {
                var card = s.Hand[i];
                if (excludeId != null && card.Id == excludeId)
                    continue;
                if (!CombatCardCost.CanAfford(card, s) || !card.IsAttack || card.Damage <= 0)
                    continue;

                if (card.IsAoe) {
                    int score = CombatDamageCalc.OutgoingDamage(card, s) * s.AliveEnemyCount;
                    if (score > bestScore) {
                        bestScore = score;
                        bestHand = i;
                        bestEnemy = -1;
                    }

                    continue;
                }

                foreach (var enemy in s.Enemies.Where(e => ThreatModel.IsViableAttackTarget(s, e))) {
                    int dmg = CombatDamageCalc.OutgoingDamage(card, s, enemy.Vulnerable);
                    if (dmg <= 0) continue;

                    int score = dmg * 3;
                    if (dmg >= enemy.EffectiveHp)
                        score += 200;
                    score += enemy.IntentDamage * 4;
                    if (!enemy.IsMinion)
                        score += 50;
                    else
                        score -= 30;

                    if (score > bestScore) {
                        bestScore = score;
                        bestHand = i;
                        bestEnemy = enemy.Index;
                    }
                }
            }

            if (bestHand < 0)
                break;

            s = CombatSimulator.Apply(s, new SimCombatAction(SimActionKind.PlayCard, bestHand, bestEnemy));
            played = true;
        }

        return s;
    }

    static bool AppliesVulnerable(CombatHandCard card) =>
        card.Profile.AppliedVulnerable > 0
        || card.Profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable);

    static bool AppliesVulnerable(CardMechanicProfile profile) =>
        profile.AppliedVulnerable > 0
        || profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable);
}
