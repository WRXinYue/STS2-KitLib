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

        var attackHand = vulnCardIndex >= 0 ? HandWithoutIndex(hand, vulnCardIndex) : hand;
        var immediateDamage = CapFollowupForTarget(
            CombatCardStats.EstimateFollowupAttackDamage(attackHand, energy), targetEnemy);
        var energyAfter = energy - vulnCost;
        if (energyAfter < 0) return 0;

        var followupDamage = CapFollowupForTarget(
            CombatCardStats.EstimateFollowupAttackDamage(attackHand, energyAfter), targetEnemy);
        var vulnHit = vulnCard != null ? CombatCardStats.ResolveDamage(vulnCard) : 0;
        var setupPayoff = vulnHit + (int)Math.Round(followupDamage * 1.5f);
        var value = setupPayoff - immediateDamage + vulnStacks * 4;

        var canLethal = LethalChecker.CanLethal(snapshot, out _);
        var incoming = IntentCalculator.TotalIncomingDamage(snapshot);
        var net = IntentCalculator.NetDamageAfterBlock(snapshot);
        var urgency = IntentCalculator.BlockUrgency(snapshot);

        if (!canLethal && incoming > 0)
            value += urgency / 4 + net / 3;

        if (targetEnemy != null) {
            var hp = targetEnemy["currentHp"]?.GetValue<int>() ?? 0;
            var maxHp = targetEnemy["maxHp"]?.GetValue<int>() ?? 1;
            if (maxHp > 0 && hp <= maxHp * 0.3f && canLethal)
                value = value * 2 / 3;
        }

        return Math.Max(0, value);
    }

    public static int ComputeVulnerableSetupValue(CombatState state, int handIndex, int enemyIndex) {
        if (handIndex < 0 || handIndex >= state.Hand.Count)
            return 0;

        var card = state.Hand[handIndex];
        if (!card.CanPlay || card.Cost > state.Energy)
            return 0;
        if (!AppliesVulnerable(card))
            return 0;

        var stacks = Math.Max(card.Profile.AppliedVulnerable, 1);
        var target = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == enemyIndex);
        if (target == null)
            return 0;
        if (target.Vulnerable > 0)
            return 0;

        if (target.IsMinion && state.Enemies.Any(e => e.IsAlive && !e.IsMinion))
            return 0;

        if (ShouldSkipVulnerableForKillLine(state, handIndex, enemyIndex))
            return 0;

        var hand = state.ToHandJson();
        var attackHand = HandWithoutIndex(hand, handIndex);
        var immediate = CapFollowupForTarget(
            CombatCardStats.EstimateFollowupAttackDamage(attackHand, state.Energy), target);
        var energyAfter = state.Energy - card.Cost;
        if (energyAfter < 0) return 0;

        var followup = CapFollowupForTarget(
            CombatCardStats.EstimateFollowupAttackDamage(attackHand, energyAfter), target);
        var payoff = card.Damage + (int)Math.Round(followup * 1.5f);
        var value = payoff - immediate + stacks * 4;

        if (ThreatModel.IncomingDamage(state) > 0 && !ThreatModel.IsFatalIfUnblocked(state))
            value += stacks * 2;

        if (enemyIndex == PrimaryAttackTargetIndex(state))
            value += stacks * 3 + 6;

        return Math.Max(0, value);
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
                if (!ShouldSkipVulnerableForKillLine(state, i, enemy.Index)) {
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

    /// <summary>Greedy kill count when attacks can be routed across enemies (excludes one hand card).</summary>
    public static int EstimateGreedyKillCount(CombatState state, int excludeHandIndex = -1) {
        var hp = state.Enemies
            .Where(e => e.IsAlive)
            .ToDictionary(e => e.Index, e => e.EffectiveHp);

        if (hp.Count == 0)
            return 0;

        var attacks = new List<(int Cost, int Damage, bool IsAoe)>();
        for (int i = 0; i < state.Hand.Count; i++) {
            if (i == excludeHandIndex)
                continue;

            var card = state.Hand[i];
            if (!CombatCardCost.CanAfford(card, state) || !card.IsAttack || card.Damage <= 0)
                continue;

            attacks.Add((
                CombatCardCost.EffectiveCost(card, state.Modifiers),
                CombatDamageCalc.OutgoingDamage(card, state, 0),
                card.IsAoe));
        }

        attacks.Sort((a, b) => b.Damage.CompareTo(a.Damage));

        int energy = state.Energy;
        int kills = 0;
        foreach (var (cost, damage, isAoe) in attacks) {
            if (cost > energy || damage <= 0)
                continue;

            energy -= cost;
            if (isAoe) {
                foreach (var idx in hp.Keys.ToList()) {
                    if (damage >= hp[idx]) {
                        kills++;
                        hp.Remove(idx);
                    }
                    else {
                        hp[idx] -= damage;
                    }
                }

                continue;
            }

            int? killIdx = null;
            foreach (var kv in hp.OrderBy(e => {
                var enemy = state.Enemies.FirstOrDefault(en => en.Index == e.Key);
                return enemy?.IsMinion == true ? 1 : 0;
            }).ThenByDescending(e => {
                var enemy = state.Enemies.FirstOrDefault(en => en.Index == e.Key);
                return enemy?.IntentDamage ?? 0;
            }).ThenBy(e => e.Value)) {
                if (damage >= kv.Value) {
                    killIdx = kv.Key;
                    break;
                }
            }

            if (killIdx != null) {
                kills++;
                hp.Remove(killIdx.Value);
                continue;
            }

            var chip = hp.OrderBy(e => e.Value).First();
            hp[chip.Key] = Math.Max(0, chip.Value - damage);
        }

        return kills;
    }

    static bool ShouldSkipVulnerableForKillLine(CombatState state, int vulnHandIndex, int vulnEnemyIndex) {
        if (CanFocusKillPrimaryWithoutVuln(state, vulnHandIndex))
            return true;

        if (state.AliveEnemyCount < 2)
            return false;

        int killsWithout = EstimateGreedyKillCount(state, vulnHandIndex);
        if (killsWithout <= 0)
            return false;

        var afterVuln = CombatSimulator.Apply(
            state,
            new SimCombatAction(SimActionKind.PlayCard, vulnHandIndex, vulnEnemyIndex));
        int killsWithVulnFirst = EstimateGreedyKillCount(afterVuln);
        if (killsWithVulnFirst < killsWithout)
            return true;

        if (killsWithVulnFirst == killsWithout) {
            int damageWithout = EstimateGreedyTotalDamage(state, vulnHandIndex);
            int damageWith = EstimateGreedyTotalDamage(afterVuln);
            if (damageWith <= damageWithout)
                return true;

            int incomingWithout = IncomingAfterGreedyAttacks(state, vulnHandIndex);
            int incomingWith = IncomingAfterGreedyAttacks(afterVuln);
            if (incomingWith > incomingWithout)
                return true;
        }

        return false;
    }

    /// <summary>True when attacks (excluding vuln card) can kill the primary non-minion this turn.</summary>
    static bool CanFocusKillPrimaryWithoutVuln(CombatState state, int excludeHandIndex) {
        var primaryIdx = PrimaryAttackTargetIndex(state);
        var primary = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == primaryIdx);
        if (primary == null || primary.IsMinion)
            return false;

        var attacks = CollectGreedyAttacks(state, excludeHandIndex);
        if (attacks.Count == 0)
            return false;

        int energy = state.Energy;
        int total = 0;
        foreach (var (_, cost, damage, _) in attacks) {
            if (cost > energy)
                continue;
            energy -= cost;
            total += CombatDamageCalc.OutgoingDamage(damage, state.Modifiers, primary.Vulnerable);
            if (total >= primary.EffectiveHp)
                return true;
        }

        return false;
    }

    static int IncomingAfterGreedyAttacks(CombatState state, int excludeHandIndex = -1) {
        var after = SimulateGreedyAttacks(state, excludeHandIndex);
        return ThreatModel.IncomingDamage(after);
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

    static List<(int HandIndex, int Cost, int Damage, bool IsAoe)> CollectGreedyAttacks(
        CombatState state,
        int excludeHandIndex) {
        var attacks = new List<(int, int, int, bool)>();
        for (int i = 0; i < state.Hand.Count; i++) {
            if (i == excludeHandIndex)
                continue;

            var card = state.Hand[i];
            if (!CombatCardCost.CanAfford(card, state) || !card.IsAttack || card.Damage <= 0)
                continue;

            attacks.Add((
                i,
                CombatCardCost.EffectiveCost(card, state.Modifiers),
                card.Damage,
                card.IsAoe));
        }

        attacks.Sort((a, b) => CombatDamageCalc
            .OutgoingDamage(b.Item3, state.Modifiers)
            .CompareTo(CombatDamageCalc.OutgoingDamage(a.Item3, state.Modifiers)));
        return attacks;
    }

    static int EstimateGreedyTotalDamage(CombatState state, int excludeHandIndex = -1) {
        var hp = state.Enemies
            .Where(e => e.IsAlive)
            .ToDictionary(e => e.Index, e => e.EffectiveHp);

        var attacks = new List<(int Cost, int Damage, bool IsAoe)>();
        for (int i = 0; i < state.Hand.Count; i++) {
            if (i == excludeHandIndex)
                continue;

            var card = state.Hand[i];
            if (!CombatCardCost.CanAfford(card, state) || !card.IsAttack || card.Damage <= 0)
                continue;

            attacks.Add((
                CombatCardCost.EffectiveCost(card, state.Modifiers),
                CombatDamageCalc.OutgoingDamage(card, state, 0),
                card.IsAoe));
        }

        attacks.Sort((a, b) => b.Damage.CompareTo(a.Damage));

        int energy = state.Energy;
        int total = 0;
        foreach (var (cost, damage, isAoe) in attacks) {
            if (cost > energy || damage <= 0)
                continue;

            energy -= cost;
            if (isAoe) {
                total += damage * hp.Count;
                continue;
            }

            total += damage;
        }

        return total;
    }

    static int CapFollowupForTarget(int rawDamage, JsonObject? targetEnemy) {
        if (targetEnemy == null)
            return rawDamage;

        var hp = targetEnemy["currentHp"]?.GetValue<int>() ?? 0;
        var block = targetEnemy["block"]?.GetValue<int>() ?? 0;
        return Math.Min(rawDamage, Math.Max(0, hp + block));
    }

    static int CapFollowupForTarget(int rawDamage, CombatEnemy target) =>
        Math.Min(rawDamage, Math.Max(0, target.EffectiveHp));

    static bool AppliesVulnerable(CombatHandCard card) =>
        card.Profile.AppliedVulnerable > 0
        || card.Profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable);

    static bool AppliesVulnerable(CardMechanicProfile profile) =>
        profile.AppliedVulnerable > 0
        || profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable);

    static JsonArray HandWithoutIndex(JsonArray hand, int skipIndex) {
        var arr = new JsonArray();
        for (int i = 0; i < hand.Count; i++) {
            if (i == skipIndex) continue;
            if (hand[i]?.DeepClone() is JsonNode clone)
                arr.Add(clone);
        }

        return arr;
    }
}
