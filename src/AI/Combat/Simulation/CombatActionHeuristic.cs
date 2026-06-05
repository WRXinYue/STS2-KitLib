using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

/// <summary>Fast move ordering for beam expansion — setup before attacks, kills before chip.</summary>
internal static class CombatActionHeuristic {
    public static int QuickScore(CombatState state, SimCombatAction action) {
        if (action.Kind == SimActionKind.EndTurn)
            return ScoreEndTurn(state);

        if (action.Kind == SimActionKind.UsePotion)
            return ScorePotionUse(state, action);

        if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count)
            return int.MinValue;

        var card = state.Hand[action.HandIndex];
        if (!CombatCardCost.CanAfford(card, state))
            return int.MinValue;

        if (DeckPollutionEvaluator.IsHandJunk(card)) {
            var emergency = DeckPollutionEvaluator.EmergencyJunkPlayScore(state, card);
            if (emergency > int.MinValue + 1)
                return emergency;
            return int.MinValue;
        }

        var junkRelief = DeckPollutionEvaluator.JunkReliefScore(state, card);
        if (junkRelief > 0)
            return junkRelief + (card.Block > 0 ? ScoreBlock(state, card) / 3 : 0);

        if (CombatTransformSimulator.IsHandAttackTransform(card.Profile))
            return ScoreHandTransform(state, action, card);

        if (AppliesVulnerable(card))
            return ScoreVulnerableSetup(state, action, card);

        if (card.IsAttack && card.Damage > 0) {
            if (ShouldPruneIllusionAttack(state, action))
                return int.MinValue;
            return ScoreAttack(state, action, card);
        }

        if (card.Block > 0)
            return ScoreBlock(state, card);

        if (card.Profile.AppliedWeak > 0)
            return 28 + card.Profile.AppliedWeak * 6;

        if (MechanicCombatBonus.IsSetupSkill(card.Profile))
            return 22;

        if (ScoresAsPileSkill(card))
            return ScorePileSkill(state, card);

        return 8;
    }

    static bool ScoresAsPileSkill(CombatHandCard card) {
        if (DeckPollutionEvaluator.IsHandJunk(card))
            return false;

        if (card.Profile.Flags.HasFlag(CardMechanicFlags.HasDraw))
            return true;
        if (card.Profile.Flags.HasFlag(CardMechanicFlags.HasExhaustFromHand))
            return true;
        return CardPileEffectResolver.DrawCount(card.Id) > 0
            || CardPileEffectResolver.ExhaustHandCount(card.Id) > 0;
    }

    static int ScorePileSkill(CombatState state, CombatHandCard card) {
        int draw = CardPileEffectResolver.DrawCount(card.Id);
        int exhaustHand = CardPileEffectResolver.ExhaustHandCount(card.Id);
        int junk = DeckPollutionEvaluator.HandJunkCount(state);

        int score = 18 + draw * 10;
        if (exhaustHand > 0) {
            if (junk > 0)
                score += junk * CombatJunkCard.DefaultJunkValue;
            else
                score -= 12;
        }

        if (draw > 0 && state.Hand.Count <= 3)
            score += 8;

        return score;
    }

    public static bool ShouldPrune(CombatState state, SimCombatAction action) {
        if (PreservesLethalPotential(state, action))
            return false;
        return QuickScore(state, action) <= int.MinValue + 1;
    }

    static bool PreservesLethalPotential(CombatState state, SimCombatAction action) {
        if (action.Kind == SimActionKind.UsePotion) {
            var afterPotion = CombatSimulator.Apply(state, action);
            return afterPotion.AliveEnemyCount == 0
                || SimLethalChecker.CanLethal(afterPotion, out _);
        }

        if (action.Kind != SimActionKind.PlayCard
            || action.HandIndex < 0
            || action.HandIndex >= state.Hand.Count)
            return false;

        if (action.EnemyIndex >= 0
            && SimLethalChecker.CanKillEnemyThisAction(state, action.HandIndex, action.EnemyIndex))
            return true;

        var card = state.Hand[action.HandIndex];
        if (card.IsAoe && card.Damage > 0 && AoeDamageEstimator.CanAoeLethalAll(state))
            return true;

        var after = CombatSimulator.Apply(state, action);
        if (after.AliveEnemyCount == 0)
            return true;

        return SimLethalChecker.CanLethal(after, out _);
    }

    static int ScoreHandTransform(CombatState state, SimCombatAction action, CombatHandCard card) {
        var after = CombatSimulator.Apply(state, action);
        if (SimLethalChecker.CanLethal(after, out _))
            return 200;

        var net = ThreatModel.NetDamageAfterBlock(state);
        if (net >= BlockThreatEvaluator.LateBlockThreshold
            && BlockDefensePolicy.NeedsBlock(state)) {
            if (!SimLethalChecker.CanSecureKillThisTurn(after))
                return int.MinValue;
        }

        var hand = state.ToHandJson();
        var delta = CombatTransformSimulator.EstimateTurnDamageDelta(hand, card.ToJson(), state.Energy);
        if (delta <= 0)
            return int.MinValue;

        var attacks = CombatTransformSimulator.CountTransformableAttacks(hand);
        var score = 40 + delta;
        if (attacks >= 2)
            score += 100;

        var afterMid = SimulateGreedyPlaysForHeuristic(after);
        var lineBefore = CombatSetupEvaluator.EvaluateLine(state);
        var lineAfter = CombatSetupEvaluator.EvaluateLine(afterMid);
        score += CombatSetupEvaluator.CompareLines(lineBefore, lineAfter) * 8;

        return score;
    }

    static CombatState SimulateGreedyPlaysForHeuristic(CombatState state) {
        var s = state;
        for (int i = 0; i < s.Hand.Count; i++) {
            var card = s.Hand[i];
            if (!CombatCardCost.CanAfford(card, s))
                continue;
            if (!CombatTransformSimulator.IsHandAttackTransform(card.Profile))
                continue;
            if (CombatTransformSimulator.EstimateTurnDamageDelta(
                    s.ToHandJson(), card.ToJson(), s.Energy) <= 0)
                continue;
            s = CombatSimulator.Apply(s, new SimCombatAction(SimActionKind.PlayCard, i, -1));
            break;
        }

        return GreedyAttacksOnce(s);
    }

    static CombatState GreedyAttacksOnce(CombatState state) {
        var s = state;
        int primary = CombatSetupEvaluator.PrimaryAttackTargetIndex(s);

        for (int i = 0; i < s.Hand.Count; i++) {
            var card = s.Hand[i];
            if (!CombatCardCost.CanAfford(card, s) || !card.IsAttack || card.Damage <= 0)
                continue;

            var focusEnemy = s.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == primary);
            int slack = focusEnemy != null ? ThreatModel.IncomingTradeSlack(focusEnemy) : 0;

            SimCombatAction? bestAction = null;
            int bestIncoming = int.MaxValue;
            int bestFuture0 = int.MaxValue;
            int bestFuture1 = int.MaxValue;
            int bestFuture2 = int.MaxValue;
            int bestScore = int.MinValue;
            bool bestHitsPrimary = false;

            foreach (var enemy in CombatSetupEvaluator.OrderEnemiesByThreat(s)) {
                int dmg = CombatDamageCalc.OutgoingDamage(card, s, enemy.Vulnerable);
                if (dmg <= 0) continue;

                var next = CombatSimulator.Apply(
                    s, new SimCombatAction(SimActionKind.PlayCard, i, enemy.Index));
                bool hitsPrimary = enemy.Index == primary;
                var future = FuturePressureFromMidTurnHeuristic(next);
                int incoming = ThreatModel.IncomingDamage(next);
                int score = ScoreMidTurnHeuristic(next);

                if (PreferGreedyAttackTarget(
                        incoming, future.f0, future.f1, future.f2, score, hitsPrimary,
                        bestIncoming, bestFuture0, bestFuture1, bestFuture2, bestScore, bestHitsPrimary,
                        slack)) {
                    bestIncoming = incoming;
                    bestFuture0 = future.f0;
                    bestFuture1 = future.f1;
                    bestFuture2 = future.f2;
                    bestScore = score;
                    bestHitsPrimary = hitsPrimary;
                    bestAction = new SimCombatAction(SimActionKind.PlayCard, i, enemy.Index);
                }
            }

            if (bestAction != null)
                return CombatSimulator.Apply(s, bestAction);
        }

        return s;
    }

    static (int f0, int f1, int f2) FuturePressureFromMidTurnHeuristic(CombatState state) {
        var afterPhase = CombatTurnResolver.ProjectAfterEnemyPhase(state);
        return (
            ThreatModel.PressureAtIntentStep(afterPhase, 0),
            ThreatModel.PressureAtIntentStep(afterPhase, 1),
            ThreatModel.PressureAtIntentStep(afterPhase, 2));
    }

    static int ScoreMidTurnHeuristic(CombatState s) {
        int incoming = ThreatModel.IncomingDamage(s);
        var afterPhase = CombatTurnResolver.ProjectAfterEnemyPhase(s);
        int future0 = ThreatModel.PressureAtIntentStep(afterPhase, 0);
        int future1 = ThreatModel.PressureAtIntentStep(afterPhase, 1);
        int enemyHp = s.Enemies.Where(e => e.IsAlive).Sum(e => e.EffectiveHp);
        return -incoming * 1000 - future0 * 250 - future1 * 100 - enemyHp;
    }

    static bool PreferGreedyAttackTarget(
        int incoming,
        int future0,
        int future1,
        int future2,
        int score,
        bool hitsPrimary,
        int bestIncoming,
        int bestFuture0,
        int bestFuture1,
        int bestFuture2,
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

        if (hitsPrimary != bestHitsPrimary)
            return hitsPrimary;

        int futureCmp = ThreatModel.CompareFutureIncoming(
            future0, future1, future2,
            bestFuture0, bestFuture1, bestFuture2);
        if (futureCmp != 0)
            return futureCmp > 0;

        return score > bestScore;
    }

    static int ScoreVulnerableSetup(CombatState state, SimCombatAction action, CombatHandCard card) {
        if (action.EnemyIndex >= 0) {
            var value = CombatSetupEvaluator.ComputeVulnerableSetupValue(
                state, action.HandIndex, action.EnemyIndex);
            if (value <= 0) return int.MinValue + 4;
            return value + card.Damage * 2;
        }

        int best = 0;
        foreach (var enemyIndex in OrderedAttackTargets(state)) {
            var value = CombatSetupEvaluator.ComputeVulnerableSetupValue(
                state, action.HandIndex, enemyIndex);
            if (value > best)
                best = value;
        }

        if (best <= 0) return int.MinValue + 4;
        return best + card.Damage * 2;
    }

    static int ScoreAttack(CombatState state, SimCombatAction action, CombatHandCard card) {
        var score = card.Damage * 3;
        var net = ThreatModel.NetDamageAfterBlock(state);
        var target = ResolveTarget(state, action.EnemyIndex);
        if (target != null) {
            if (target.Vulnerable <= 0) {
                var setupValue = CombatSetupEvaluator.ComputeBestVulnerableDeferValue(
                    BuildSnapshot(state), state.ToHandJson(), state.Energy, EnemyToJson(target));
                score -= setupValue;
            }

            var eff = EffectiveDamage(card.Damage, target);
            if (eff >= target.EffectiveHp)
                score += 220;
            score += Math.Max(0, 60 - target.EffectiveHp);
            score += target.IntentDamage * 3;
            if (target.IsMinion)
                score -= 25;

            score += ThreatEconomy.KillBeforeHitBonus(target, state);

            if (action.EnemyIndex == CombatSetupEvaluator.PrimaryAttackTargetIndex(state))
                score += 40;

            if (target.IsMinion && eff >= target.EffectiveHp
                && state.Enemies.Any(e => e.IsAlive && !e.IsMinion && e.Index != target.Index))
                score -= 60;
        }

        if (card.IsAoe) {
            int kills = AoeDamageEstimator.EstimateAoeKills(state, card.Damage);
            score += kills * 80;
        }

        score -= UnsafeAttackPenalty(state, action, card, net);

        if (ThreatModel.IsFatalIfUnblocked(state) && net > card.Damage)
            score -= 40;

        return score;
    }

    /// <summary>Heavy penalty while net damage is uncovered; kills/AOE wipes exempt.</summary>
    static int UnsafeAttackPenalty(
        CombatState state,
        SimCombatAction action,
        CombatHandCard card,
        int net) {
        if (net <= 0)
            return 0;

        if (card.IsAoe && AoeDamageEstimator.CanAoeLethalAll(state))
            return net * 2;

        if (action.EnemyIndex >= 0
            && SimLethalChecker.CanKillEnemyThisAction(state, action.HandIndex, action.EnemyIndex))
            return net * 2;

        if (SimLethalChecker.CanSecureKillThisTurn(state))
            return net * 2;

        if (!BlockDefensePolicy.NeedsBlock(state))
            return net * 3;

        return net * CombatEvalWeights.UnsafeAttackPenaltyPerNet;
    }

    static int ScorePotionUse(CombatState state, SimCombatAction action) {
        if (action.PotionSlot < 0)
            return int.MinValue;

        var potion = state.Potions.FirstOrDefault(p => p.Slot == action.PotionSlot);
        if (potion == null)
            return int.MinValue;

        if (!PotionCombatEffectData.TryGetProfile(potion.Id, out var profile) || !profile.Simulatable)
            return int.MinValue;

        var ctx = PotionUseScoring.FromState(state, potion.Id);
        return PotionUseScoring.ScoreSimProfile(state, profile, action.EnemyIndex, ctx);
    }

    static int ScoreBlock(CombatState state, CombatHandCard card) {
        var net = ThreatModel.NetDamageAfterBlock(state);
        if (net <= 0) return 5;

        if (SimLethalChecker.CanLethal(state, out _)
            && BlockDefensePolicy.CanSkipBlockForKill(state))
            return 4;

        var effective = Math.Min(CombatDamageCalc.OutgoingBlock(card, state), net);
        var score = 40 + effective * 5;
        if (ThreatModel.IsFatalIfUnblocked(state))
            score += 80;
        if (BlockDefensePolicy.CanFullyBlock(state))
            score += 20;
        return score;
    }

    static int ScoreEndTurn(CombatState state) {
        var playable = CombatCardCost.CountAffordable(state);
        if (playable == 0)
            return 50;

        var net = ThreatModel.NetDamageAfterBlock(state);
        if (ThreatModel.IsFatalIfUnblocked(state))
            return int.MinValue;

        if (BlockDefensePolicy.ShouldPrioritizeBlock(state))
            return int.MinValue + 1;

        if (net > 0 && state.PlayerBlock < net)
            return 5;

        if (net <= 0 && ThreatModel.IncomingDamage(state) > 0)
            return 40;

        var nextPressure = ThreatModel.ScaledNextTurnPressure(state);
        if (ThreatModel.IncomingDamage(state) == 0 && nextPressure >= 8)
            return 5 - nextPressure / 2;

        var debt = CombatSetupEvaluator.ComputeSetupDebt(state);
        var score = 15 - playable * 3 - debt;

        var junk = DeckPollutionEvaluator.HandJunkCount(state);
        if (junk > 0) {
            score -= junk * 25;
            if (DeckPollutionEvaluator.HasAffordableJunkRelief(state))
                score = int.MinValue + 2;
            else if (DeckPollutionEvaluator.HasAffordableEmergencyJunkClear(state))
                score = int.MinValue + 3;
        }

        return score;
    }

    static bool ShouldPruneIllusionAttack(CombatState state, SimCombatAction action) {
        if (action.EnemyIndex < 0)
            return false;

        var target = ResolveTarget(state, action.EnemyIndex);
        return target != null && !ThreatModel.IsViableAttackTarget(state, target);
    }

    static CombatEnemy? ResolveTarget(CombatState state, int enemyIndex) {
        if (enemyIndex < 0) return null;
        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive) continue;
            if (enemy.Index == enemyIndex)
                return enemy;
        }

        return null;
    }

    static IEnumerable<int> OrderedAttackTargets(CombatState state) =>
        CombatSetupEvaluator.OrderEnemiesByThreat(state)
            .Take(4)
            .Select(e => e.Index);

    static int EffectiveDamage(int damage, CombatEnemy target) =>
        (int)Math.Round(damage * (target.Vulnerable > 0 ? 1.5f : 1f));

    static bool AppliesVulnerable(CombatHandCard card) =>
        card.Profile.AppliedVulnerable > 0;

    static JsonObject BuildSnapshot(CombatState state) => new() {
        ["currentHp"] = state.PlayerHp,
        ["maxHp"] = state.PlayerMaxHp,
        ["combat"] = new JsonObject {
            ["playerBlock"] = state.PlayerBlock,
            ["currentEnergy"] = state.Energy,
            ["enemies"] = new JsonArray(state.Enemies.Select(EnemyToJson).ToArray()),
        },
    };

    static JsonObject EnemyToJson(CombatEnemy enemy) => new() {
        ["index"] = enemy.Index,
        ["currentHp"] = enemy.CurrentHp,
        ["maxHp"] = enemy.MaxHp,
        ["block"] = enemy.Block,
        ["isAlive"] = enemy.IsAlive,
        ["intentDamage"] = enemy.IntentDamage,
        ["powers"] = enemy.Vulnerable > 0
            ? new JsonArray(new JsonObject { ["id"] = "VULNERABLE", ["amount"] = enemy.Vulnerable })
            : new JsonArray(),
    };
}
