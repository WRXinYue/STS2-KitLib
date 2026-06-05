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

    /// <summary>Lexicographic: this-turn incoming, future pressure, focus-target HP, total enemy HP, player HP.</summary>
    public readonly record struct CombatLineOutcome(
        int Incoming,
        int FutureIncoming0,
        int FutureIncoming1,
        int FutureIncoming2,
        int FocusHp,
        int EnemyHp,
        int PlayerHpAfterTurn);

    /// <summary>Greedy completion of the turn from a mid-turn state, then outcome metrics.</summary>
    public static CombatLineOutcome EvaluateLine(CombatState midTurn) =>
        EvaluateLineOutcome(SimulateGreedyPlays(midTurn));

    /// <summary>Positive when <paramref name="candidate"/> is better than <paramref name="baseline"/>.</summary>
    public static int CompareLines(CombatLineOutcome baseline, CombatLineOutcome candidate) {
        if (candidate.Incoming != baseline.Incoming) {
            int incomingCmp = CompareIncomingTrade(
                baseline.Incoming, candidate.Incoming,
                baseline.FocusHp, candidate.FocusHp,
                baseline.FutureIncoming0, candidate.FutureIncoming0,
                baseline.FutureIncoming1, candidate.FutureIncoming1,
                baseline.FutureIncoming2, candidate.FutureIncoming2);
            if (incomingCmp != 0)
                return incomingCmp;
        }

        int futureCmp = ThreatModel.CompareFutureIncoming(
            candidate.FutureIncoming0, candidate.FutureIncoming1, candidate.FutureIncoming2,
            baseline.FutureIncoming0, baseline.FutureIncoming1, baseline.FutureIncoming2);
        if (futureCmp != 0)
            return futureCmp;

        if (candidate.FocusHp != baseline.FocusHp)
            return baseline.FocusHp - candidate.FocusHp;

        if (candidate.EnemyHp != baseline.EnemyHp)
            return baseline.EnemyHp - candidate.EnemyHp;
        return candidate.PlayerHpAfterTurn - baseline.PlayerHpAfterTurn;
    }

    /// <summary>Positive when candidate is better despite possibly higher this-turn incoming.</summary>
    static int CompareIncomingTrade(
        int baselineIncoming,
        int candidateIncoming,
        int baselineFocusHp,
        int candidateFocusHp,
        int baselineF0,
        int candidateF0,
        int baselineF1,
        int candidateF1,
        int baselineF2,
        int candidateF2) {
        if (candidateIncoming == baselineIncoming)
            return 0;
        if (candidateIncoming < baselineIncoming)
            return 1;

        int extra = candidateIncoming - baselineIncoming;
        int futureRelief = (baselineF0 - candidateF0) + (baselineF1 - candidateF1) + (baselineF2 - candidateF2);
        int focusGain = baselineFocusHp - candidateFocusHp;
        int benefit = focusGain + futureRelief;
        if (extra <= benefit && benefit > 0)
            return 1;
        return baselineIncoming - candidateIncoming;
    }

    public static int LineRankScore(CombatLineOutcome outcome, ThreatModel.LineScoreWeights weights) {
        long cap = Math.Max(weights.IncomingCap, 1);
        long score =
            (cap - Math.Min(outcome.Incoming, cap)) * weights.IncomingUnit * cap * cap
            + (cap - Math.Min(outcome.FutureIncoming0, cap)) * weights.FutureUnit * cap
            + (cap - Math.Min(outcome.FutureIncoming1, cap)) * Math.Max(1, weights.FutureUnit / 2)
            + (cap - Math.Min(outcome.FutureIncoming2, cap)) * Math.Max(1, weights.FutureUnit / 4)
            + (cap - Math.Min(outcome.FocusHp, cap)) * weights.FocusUnit
            + (cap * 10 - Math.Min(outcome.EnemyHp, cap * 10))
            + outcome.PlayerHpAfterTurn;
        return score > int.MaxValue ? int.MaxValue - 1 : (int)score;
    }

    public static CombatLineOutcome WipeOutcome(CombatState state) =>
        new(0, 0, 0, 0, 0, 0, state.PlayerHp);

    static int CompareLineOutcome(CombatLineOutcome without, CombatLineOutcome with) =>
        CompareLines(without, with);

    static CombatLineOutcome EvaluateLineOutcome(CombatState midTurn) {
        var afterTurn = CombatTurnResolver.ResolveEndTurn(midTurn);
        int focusIdx = PrimaryAttackTargetIndex(midTurn);
        var focusMid = midTurn.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == focusIdx);
        return new CombatLineOutcome(
            ThreatModel.IncomingDamage(midTurn),
            ThreatModel.PressureAtIntentStep(afterTurn, 0),
            ThreatModel.PressureAtIntentStep(afterTurn, 1),
            ThreatModel.PressureAtIntentStep(afterTurn, 2),
            focusMid?.CurrentHp ?? 0,
            afterTurn.Enemies.Where(e => e.IsAlive).Sum(e => e.CurrentHp),
            afterTurn.PlayerHp);
    }

    static int ScoreMidTurn(CombatState s) =>
        ThreatModel.MidTurnScore(s, PrimaryAttackTargetIndex(s));

    /// <summary>Sim delta: play vuln first vs skip vuln card, compared by explicit line metrics.</summary>
    static int ComputeVulnerableSetupSimDelta(CombatState state, int handIndex, int enemyIndex) {
        var withoutMid = SimulateGreedyPlays(state, handIndex);
        var afterVuln = CombatSimulator.Apply(
            state,
            new SimCombatAction(SimActionKind.PlayCard, handIndex, enemyIndex));
        var withMid = SimulateGreedyPlays(afterVuln);

        var without = EvaluateLineOutcome(withoutMid);
        var with = EvaluateLineOutcome(withMid);
        int delta = CompareLineOutcome(without, with);

        // #region agent log
        AgentDebugLog.Write("H1", "CombatSetupEvaluator.ComputeVulnerableSetupSimDelta", "vuln sim delta", new {
            cardId = state.Hand[handIndex].Id,
            enemyIndex,
            withoutIncoming = without.Incoming,
            withIncoming = with.Incoming,
            withoutFuture0 = without.FutureIncoming0,
            withFuture0 = with.FutureIncoming0,
            withoutFuture1 = without.FutureIncoming1,
            withFuture1 = with.FutureIncoming1,
            withoutFuture2 = without.FutureIncoming2,
            withFuture2 = with.FutureIncoming2,
            withoutEnemyHp = without.EnemyHp,
            withEnemyHp = with.EnemyHp,
            withoutPlayerHp = without.PlayerHpAfterTurn,
            withPlayerHp = with.PlayerHpAfterTurn,
            delta,
            energy = state.Energy,
            hand = state.Hand.Select(c => c.Id).ToArray(),
            enemies = state.Enemies.Where(e => e.IsAlive).Select(e => new {
                e.Index, e.MonsterId, e.CurrentHp, e.IsMinion, e.IntentDamage,
            }).ToArray(),
        });
        // #endregion

        return delta;
    }

    static CombatState SimulateGreedyPlays(CombatState state, int excludeHandIndex = -1) {
        var s = state;
        string? excludeId = excludeHandIndex >= 0 && excludeHandIndex < state.Hand.Count
            ? state.Hand[excludeHandIndex].Id
            : null;

        bool playedTransform = true;
        while (playedTransform) {
            playedTransform = false;
            int bestHand = -1;
            int bestDelta = 0;

            for (int i = 0; i < s.Hand.Count; i++) {
                var card = s.Hand[i];
                if (excludeId != null && card.Id == excludeId)
                    continue;
                if (!CombatCardCost.CanAfford(card, s))
                    continue;
                if (!CombatTransformSimulator.IsHandAttackTransform(card.Profile))
                    continue;

                int delta = CombatTransformSimulator.EstimateTurnDamageDelta(
                    s.ToHandJson(), card.ToJson(), s.Energy);
                if (delta > bestDelta) {
                    bestDelta = delta;
                    bestHand = i;
                }
            }

            if (bestHand < 0 || bestDelta <= 0)
                break;

            s = CombatSimulator.Apply(s, new SimCombatAction(SimActionKind.PlayCard, bestHand, -1));
            playedTransform = true;
        }

        return SimulateGreedyAttacks(s, excludeHandIndex);
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

    /// <summary>Threat-ordered enemies for focus fire (future peak threat and HP over this-turn poke).</summary>
    public static IEnumerable<CombatEnemy> OrderEnemiesByThreat(CombatState state) =>
        state.Enemies
            .Where(e => ThreatModel.IsViableAttackTarget(state, e))
            .OrderByDescending(e => e.IsMinion ? 0 : 1)
            .ThenByDescending(e => ThreatModel.FocusThreatScore(e, state))
            .ThenByDescending(e => e.CurrentHp)
            .ThenBy(e => e.EffectiveHp);

    /// <summary>Primary focus for single-target attacks this turn.</summary>
    public static int PrimaryAttackTargetIndex(CombatState state) =>
        OrderEnemiesByThreat(state).Select(e => e.Index).FirstOrDefault();

    public static int FocusHpAfter(CombatState state, int focusIndex) =>
        ThreatModel.FocusHp(state, focusIndex);

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

        int focusIndex = PrimaryAttackTargetIndex(state);
        int incomingSlack = 0;

        while (true) {
            var focusEnemy = s.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == focusIndex);
            if (focusEnemy == null) {
                focusIndex = PrimaryAttackTargetIndex(s);
                focusEnemy = s.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == focusIndex);
            }

            incomingSlack = focusEnemy != null ? ThreatModel.IncomingTradeSlack(focusEnemy, s) : 0;

            SimCombatAction? bestAction = null;
            int bestScore = int.MinValue;
            int bestIncoming = int.MaxValue;
            int bestFuture0 = int.MaxValue;
            int bestFuture1 = int.MaxValue;
            int bestFuture2 = int.MaxValue;
            int bestFocusHp = int.MaxValue;
            bool bestHitsPrimary = false;
            int primary = focusIndex;

            for (int i = 0; i < s.Hand.Count; i++) {
                var card = s.Hand[i];
                if (excludeId != null && card.Id == excludeId)
                    continue;
                if (!CombatCardCost.CanAfford(card, s) || !card.IsAttack || card.Damage <= 0)
                    continue;

                if (card.IsAoe) {
                    var next = CombatSimulator.Apply(s, new SimCombatAction(SimActionKind.PlayCard, i, -1));
                    var future = FuturePressureFromMidTurn(next);
                    if (IsBetterAttackStep(
                            ThreatModel.IncomingDamage(next),
                            future.f0, future.f1, future.f2,
                            ScoreMidTurn(next),
                            FocusHpAfter(next, primary),
                            hitsPrimary: false,
                            bestIncoming, bestFuture0, bestFuture1, bestFuture2,
                            bestFocusHp, bestScore, bestHitsPrimary,
                            incomingSlack)) {
                        bestScore = ScoreMidTurn(next);
                        bestIncoming = ThreatModel.IncomingDamage(next);
                        bestFuture0 = future.f0;
                        bestFuture1 = future.f1;
                        bestFuture2 = future.f2;
                        bestFocusHp = FocusHpAfter(next, primary);
                        bestHitsPrimary = false;
                        bestAction = new SimCombatAction(SimActionKind.PlayCard, i, -1);
                    }

                    continue;
                }

                foreach (var enemy in OrderEnemiesByThreat(s)) {
                    int dmg = CombatDamageCalc.OutgoingDamage(card, s, enemy.Vulnerable);
                    if (dmg <= 0) continue;

                    var next = CombatSimulator.Apply(
                        s, new SimCombatAction(SimActionKind.PlayCard, i, enemy.Index));
                    bool hitsPrimary = enemy.Index == primary;
                    var future = FuturePressureFromMidTurn(next);
                    int focusHp = FocusHpAfter(next, primary);
                    if (IsBetterAttackStep(
                            ThreatModel.IncomingDamage(next),
                            future.f0, future.f1, future.f2,
                            ScoreMidTurn(next),
                            focusHp,
                            hitsPrimary,
                            bestIncoming, bestFuture0, bestFuture1, bestFuture2,
                            bestFocusHp, bestScore, bestHitsPrimary,
                            incomingSlack)) {
                        bestScore = ScoreMidTurn(next);
                        bestIncoming = ThreatModel.IncomingDamage(next);
                        bestFuture0 = future.f0;
                        bestFuture1 = future.f1;
                        bestFuture2 = future.f2;
                        bestFocusHp = focusHp;
                        bestHitsPrimary = hitsPrimary;
                        bestAction = new SimCombatAction(SimActionKind.PlayCard, i, enemy.Index);
                    }
                }
            }

            if (bestAction == null)
                break;

            // #region agent log
            if (bestAction.EnemyIndex >= 0) {
                var tgt = s.Enemies.FirstOrDefault(e => e.Index == bestAction.EnemyIndex);
                AgentDebugLog.Write("H3", "CombatSetupEvaluator.SimulateGreedyAttacks", "greedy attack pick", new {
                    cardId = s.Hand[bestAction.HandIndex].Id,
                    enemyIndex = bestAction.EnemyIndex,
                    monsterId = tgt?.MonsterId,
                    isMinion = tgt?.IsMinion,
                    hp = tgt?.CurrentHp,
                    focusIndex = primary,
                    focusScores = s.Enemies.Where(e => e.IsAlive).Select(e => new {
                        e.Index,
                        e.MonsterId,
                        score = ThreatModel.FocusThreatScore(e, s),
                        peak = ThreatModel.PeakScheduledDamage(e),
                    }).ToArray(),
                    incomingAfter = bestIncoming,
                    future0 = bestFuture0,
                    future1 = bestFuture1,
                    future2 = bestFuture2,
                    hitsPrimary = bestHitsPrimary,
                    score = bestScore,
                });
            }
            // #endregion

            s = CombatSimulator.Apply(s, bestAction);
        }

        return s;
    }

    static (int f0, int f1, int f2) FuturePressureFromMidTurn(CombatState state) {
        var afterPhase = CombatTurnResolver.ProjectAfterEnemyPhase(state);
        return (
            ThreatModel.PressureAtIntentStep(afterPhase, 0),
            ThreatModel.PressureAtIntentStep(afterPhase, 1),
            ThreatModel.PressureAtIntentStep(afterPhase, 2));
    }

    static bool IsBetterAttackStep(
        int incoming,
        int future0,
        int future1,
        int future2,
        int score,
        int focusHp,
        bool hitsPrimary,
        int bestIncoming,
        int bestFuture0,
        int bestFuture1,
        int bestFuture2,
        int bestFocusHp,
        int bestScore,
        bool bestHitsPrimary,
        int incomingSlack) {
        if (incoming != bestIncoming) {
            if (incoming < bestIncoming) {
                if (bestHitsPrimary && !hitsPrimary && bestIncoming - incoming <= incomingSlack)
                    return false;
                return true;
            }
            if (hitsPrimary && !bestHitsPrimary && incoming - bestIncoming <= incomingSlack)
                return true;
            return false;
        }

        if (focusHp != bestFocusHp)
            return focusHp < bestFocusHp;

        if (hitsPrimary != bestHitsPrimary)
            return hitsPrimary;

        int futureCmp = ThreatModel.CompareFutureIncoming(
            future0, future1, future2,
            bestFuture0, bestFuture1, bestFuture2);
        if (futureCmp != 0)
            return futureCmp > 0;

        if (score != bestScore)
            return score > bestScore;
        return false;
    }

    static bool AppliesVulnerable(CombatHandCard card) {
        if (card.Profile.AppliedVulnerable <= 0)
            return false;
        if (string.Equals(card.Id, "GIANT_ROCK", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    static bool AppliesVulnerable(CardMechanicProfile profile) =>
        profile.AppliedVulnerable > 0;
}
