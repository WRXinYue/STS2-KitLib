using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

/// <summary>Dynamic setup-vs-attack comparison from live snapshot metrics.</summary>
internal static class CombatSetupEvaluator {
    const int IncomingTradeDamageMultiplier = 2;
    const int IncomingTradeFutureReliefDivisor = 2;
    const int IncomingTradeChipFocusDivisor = 4;
    const int InfernoMultiTargetTradeMinRatio = 4;
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

    /// <summary>Lexicographic: incoming, future pressure, pollution, focus HP, enemy HP, vuln/weak/inferno outlook, pile outlook, player HP.</summary>
    public readonly record struct CombatLineOutcome(
        int Incoming,
        int FutureIncoming0,
        int FutureIncoming1,
        int FutureIncoming2,
        int DeckPollution,
        int FocusHp,
        int EnemyHp,
        int VulnerableOutlook,
        int WeakOutlook,
        int InfernoOutlook,
        int PileOutlook,
        int PlayerHpAfterTurn,
        int PotionRetainCost);

    /// <summary>Greedy completion of the turn from a mid-turn state, then outcome metrics.</summary>
    public static CombatLineOutcome EvaluateLine(CombatState midTurn, CombatState? decisionRoot = null) {
        int potionCost = decisionRoot != null ? PotionLineCost.Estimate(decisionRoot, midTurn) : 0;
        return EvaluateLineOutcome(SimulateGreedyPlays(midTurn), potionCost);
    }

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

        if (candidate.DeckPollution != baseline.DeckPollution)
            return baseline.DeckPollution - candidate.DeckPollution;

        if (candidate.FocusHp != baseline.FocusHp)
            return baseline.FocusHp - candidate.FocusHp;

        if (candidate.EnemyHp != baseline.EnemyHp)
            return baseline.EnemyHp - candidate.EnemyHp;

        if (candidate.VulnerableOutlook != baseline.VulnerableOutlook)
            return candidate.VulnerableOutlook - baseline.VulnerableOutlook;

        if (candidate.PotionRetainCost != baseline.PotionRetainCost)
            return baseline.PotionRetainCost - candidate.PotionRetainCost;

        if (candidate.WeakOutlook != baseline.WeakOutlook)
            return candidate.WeakOutlook - baseline.WeakOutlook;

        if (candidate.InfernoOutlook != baseline.InfernoOutlook)
            return candidate.InfernoOutlook - baseline.InfernoOutlook;

        if (candidate.PileOutlook != baseline.PileOutlook)
            return candidate.PileOutlook - baseline.PileOutlook;

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
        int focusBenefit = candidateFocusHp == 0
            ? focusGain
            : focusGain / IncomingTradeChipFocusDivisor;
        int benefit = focusBenefit + futureRelief / IncomingTradeFutureReliefDivisor;
        if (extra * IncomingTradeDamageMultiplier <= benefit && benefit > 0)
            return 1;
        return baselineIncoming - candidateIncoming;
    }

    /// <summary>Fixed packing aligned with <see cref="CompareLines"/> — fits in int32 without clipping.</summary>
    public static int PackLineScore(CombatLineOutcome outcome) {
        const int cap = 250;
        long score =
            (cap - Math.Min(outcome.Incoming, cap)) * 8_000_000L
            + (cap - Math.Min(outcome.FutureIncoming0, cap)) * 8_000L
            + (cap - Math.Min(outcome.FutureIncoming1, cap)) * 800L
            + (cap - Math.Min(outcome.FutureIncoming2, cap)) * 80L
            + (cap - Math.Min(outcome.DeckPollution, cap * 4)) * 8L
            + (cap - Math.Min(outcome.FocusHp, cap))
            + (cap * 10 - Math.Min(outcome.EnemyHp, cap * 10))
            + Math.Min(outcome.VulnerableOutlook, 3000)
            + Math.Min(outcome.WeakOutlook, 2000)
            + Math.Min(outcome.InfernoOutlook, 8000)
            + Math.Min(outcome.PileOutlook, 500)
            + Math.Min(outcome.PlayerHpAfterTurn, cap) * 4_000L;
        const int packedCap = int.MaxValue - 2;
        return score > packedCap ? packedCap : (int)score;
    }

    public static int RankPlayAction(
        CombatState state,
        SimCombatAction action,
        JsonObject? rootSnapshot = null) {
        if (action.Kind == SimActionKind.EndTurn)
            return int.MinValue;

        var next = CombatSimulator.Apply(state, action);
        if (next.AliveEnemyCount == 0)
            return int.MaxValue;

        int baseScore = PackLineScore(EvaluateLine(next));
        if (action.Kind == SimActionKind.PlayCard
            && action.HandIndex >= 0
            && action.HandIndex < state.Hand.Count) {
            var card = state.Hand[action.HandIndex];
            baseScore += AttackerKillPriority.OpenerBonus(state, action);
            baseScore -= AttackerKillPriority.SetupOpenerPenalty(state, card);
        }
        return SimMoveScoring.WithModifiers(state, action, baseScore, rootSnapshot);
    }

    public static CombatLineOutcome WipeOutcome(CombatState state) =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, state.PlayerHp, 0);

    static int CompareLineOutcome(CombatLineOutcome without, CombatLineOutcome with) =>
        CompareLines(without, with);

    static CombatLineOutcome EvaluateLineOutcome(CombatState midTurn, int potionRetainCost = 0) {
        var afterTurn = CombatTurnResolver.ResolveEndTurn(midTurn);
        int focusIdx = ThreatModel.IncomingDamage(midTurn) > 0
            ? GreedyAttackFocusIndex(midTurn)
            : PrimaryAttackTargetIndex(midTurn);
        var focusMid = midTurn.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == focusIdx);
        int rawEnemyHp = afterTurn.Enemies.Where(e => e.IsAlive).Sum(e => e.CurrentHp);
        int comboEnemyCredit = InfernoComboEnemyHpCredit(midTurn);
        int focusHp = focusMid?.CurrentHp ?? 0;
        if (comboEnemyCredit > 0)
            focusHp = Math.Max(0, focusHp - comboEnemyCredit);
        return new CombatLineOutcome(
            EffectiveIncomingForLine(midTurn),
            ThreatModel.PressureAtIntentStepKillAdjusted(afterTurn, 0, afterDrawTurnStart: true),
            ThreatModel.PressureAtIntentStep(afterTurn, 1),
            ThreatModel.PressureAtIntentStep(afterTurn, 2),
            DeckPollutionEvaluator.EffectivePollutionBurden(afterTurn),
            focusHp,
            Math.Max(0, rawEnemyHp - comboEnemyCredit),
            VulnerableOutlookEvaluator.Estimate(afterTurn),
            WeakMitigationEvaluator.Estimate(afterTurn),
            0,
            PileRhythmEvaluator.DrawPileOutlook(afterTurn),
            afterTurn.PlayerHp + InfernoLinePlayerHpCredit(afterTurn),
            potionRetainCost);
    }

    /// <summary>PackLineScore weights player HP heavily; credit inferno turn-start AOE trades on multi-target turns.</summary>
    static int InfernoLinePlayerHpCredit(CombatState afterTurn) {
        var buffs = afterTurn.Buffs;
        if (buffs.InfernoRetaliation <= 0 || buffs.TurnStartSelfDamage <= 0)
            return 0;

        int enemies = ThreatModel.EffectiveAoeEnemyCount(afterTurn);
        if (enemies < 2)
            return 0;

        int tradeValue = buffs.InfernoRetaliation * enemies;
        if (tradeValue < buffs.TurnStartSelfDamage * InfernoMultiTargetTradeMinRatio)
            return 0;

        return buffs.TurnStartSelfDamage;
    }

    /// <summary>Net unblocked damage at end of the player line — always penalized in beam scoring.</summary>
    static int EffectiveIncomingForLine(CombatState midTurn) {
        if (midTurn.AliveEnemyCount == 0)
            return 0;
        return ThreatModel.NetDamageAfterBlock(midTurn);
    }

    static int ScoreMidTurn(CombatState s) =>
        ThreatModel.MidTurnScore(s, GreedyAttackFocusIndex(s));

    /// <summary>Sim delta: play vuln first vs skip vuln card, compared by explicit line metrics.</summary>
    static int ComputeVulnerableSetupSimDelta(CombatState state, int handIndex, int enemyIndex) {
        var withoutMid = SimulateGreedyPlays(state, handIndex);
        var afterVuln = CombatSimulator.Apply(
            state,
            new SimCombatAction(SimActionKind.PlayCard, handIndex, enemyIndex));
        var withMid = SimulateGreedyPlays(afterVuln);

        var without = EvaluateLineOutcome(withoutMid);
        var with = EvaluateLineOutcome(withMid);
        return CompareLineOutcome(without, with);
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

        if (BlockDefensePolicy.NeedsBlock(s))
            s = SimulateGreedyBlock(s, excludeHandIndex);

        if (s.Buffs.InfernoRetaliation > 0 && ThreatModel.EffectiveAoeEnemyCount(s) >= 2)
            s = SimulateGreedyInfernoHpLoss(s, excludeHandIndex);

        s = SimulateGreedyAttacks(s, excludeHandIndex);

        if (BlockDefensePolicy.NeedsBlock(s))
            s = SimulateGreedyBlock(s, excludeHandIndex);

        return SimulateGreedyJunkClear(s, excludeHandIndex);
    }

    /// <summary>Hp-loss skills that trigger Inferno AOE — not covered by the attack-only greedy loop.</summary>
    static CombatState SimulateGreedyInfernoHpLoss(CombatState state, int excludeHandIndex = -1) {
        var s = state;
        string? excludeId = excludeHandIndex >= 0 && excludeHandIndex < state.Hand.Count
            ? state.Hand[excludeHandIndex].Id
            : null;

        while (true) {
            SimCombatAction? bestAction = null;
            int bestEnemyHp = int.MaxValue;
            int bestScore = int.MinValue;

            for (int i = 0; i < s.Hand.Count; i++) {
                var card = s.Hand[i];
                if (excludeId != null && card.Id == excludeId)
                    continue;
                if (!CombatCardCost.CanAfford(card, s))
                    continue;
                if (card.Profile.HpLoss <= 0)
                    continue;

                int loss = card.Profile.HpLoss;
                if (loss >= ThreatModel.EffectiveHp(s))
                    continue;

                if (card.IsAttack && card.Damage > 0) {
                    foreach (var enemy in OrderEnemiesForGreedyAttacks(s)) {
                        int dmg = CombatDamageCalc.OutgoingDamage(card, s, enemy.Vulnerable);
                        if (dmg <= 0)
                            continue;

                        var action = new SimCombatAction(SimActionKind.PlayCard, i, enemy.Index);
                        var next = CombatSimulator.Apply(s, action);
                        int enemyHp = AliveEnemyHp(next);
                        int score = ScoreMidTurn(next);
                        if (enemyHp < bestEnemyHp || (enemyHp == bestEnemyHp && score > bestScore)) {
                            bestEnemyHp = enemyHp;
                            bestScore = score;
                            bestAction = action;
                        }
                    }

                    continue;
                }

                var skillNext = CombatSimulator.Apply(s, new SimCombatAction(SimActionKind.PlayCard, i, -1));
                int skillEnemyHp = AliveEnemyHp(skillNext);
                int skillScore = ScoreMidTurn(skillNext);
                if (skillEnemyHp < bestEnemyHp || (skillEnemyHp == bestEnemyHp && skillScore > bestScore)) {
                    bestEnemyHp = skillEnemyHp;
                    bestScore = skillScore;
                    bestAction = new SimCombatAction(SimActionKind.PlayCard, i, -1);
                }
            }

            if (bestAction == null)
                break;

            s = CombatSimulator.Apply(s, bestAction);
        }

        return s;
    }

    static CombatState SimulateGreedyJunkClear(CombatState state, int excludeHandIndex = -1) {
        if (DeckPollutionEvaluator.HasAffordableJunkRelief(state))
            return state;

        var s = state;
        string? excludeId = excludeHandIndex >= 0 && excludeHandIndex < state.Hand.Count
            ? state.Hand[excludeHandIndex].Id
            : null;

        while (true) {
            if (ThreatModel.IncomingDamage(s) > 0
                && DeckPollutionEvaluator.ExpectedPlayableDamage(s) > 0)
                break;

            int bestHand = -1;
            int bestDraw = -1;

            for (int i = 0; i < s.Hand.Count; i++) {
                var card = s.Hand[i];
                if (excludeId != null && card.Id == excludeId)
                    continue;
                if (!DeckPollutionEvaluator.IsHandJunk(card))
                    continue;
                if (!DeckPollutionEvaluator.SelfExhaustsOnPlay(card))
                    continue;
                if (!CombatCardCost.CanAfford(card, s))
                    continue;

                int draw = CardPileEffectResolver.DrawCount(card.Id);
                if (draw > bestDraw) {
                    bestDraw = draw;
                    bestHand = i;
                }
            }

            if (bestHand < 0)
                break;

            s = CombatSimulator.Apply(s, new SimCombatAction(SimActionKind.PlayCard, bestHand, -1));
        }

        return s;
    }

    static CombatState SimulateGreedyBlock(CombatState state, int excludeHandIndex = -1) {
        var s = state;
        string? excludeId = excludeHandIndex >= 0 && excludeHandIndex < state.Hand.Count
            ? state.Hand[excludeHandIndex].Id
            : null;

        while (ThreatModel.NetDamageAfterBlock(s) > 0) {
            int bestHand = -1;
            int bestCover = 0;

            for (int i = 0; i < s.Hand.Count; i++) {
                var card = s.Hand[i];
                if (excludeId != null && card.Id == excludeId)
                    continue;
                if (!CombatCardCost.CanAfford(card, s))
                    continue;

                int block = CombatDamageCalc.OutgoingBlock(card, s);
                if (block <= 0)
                    continue;

                int cover = Math.Min(block, ThreatModel.NetDamageAfterBlock(s));
                if (cover > bestCover) {
                    bestCover = cover;
                    bestHand = i;
                }
            }

            if (bestHand < 0 || bestCover <= 0)
                break;

            s = CombatSimulator.Apply(s, new SimCombatAction(SimActionKind.PlayCard, bestHand, -1));
        }

        return s;
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

    /// <summary>Inferno + same-turn self-damage combo available but not opened yet.</summary>
    public static int ComputeInfernoComboDebt(CombatState state) {
        if (!TryFindAffordableInferno(state, out int infernoIdx, out var inferno))
            return 0;

        int energyAfter = state.Energy - CombatCardCost.EffectiveCost(inferno, state);
        if (!HasAffordableHpLossPartner(state, infernoIdx, energyAfter, handOnly: false))
            return 0;

        int enemies = ThreatModel.EffectiveAoeEnemyCount(state);
        return 10 + Math.Max(inferno.Profile.InstallAmount(PlayerPowerEffectKind.InfernoRetaliate), 6) * enemies;
    }

    /// <summary>Beam tie-break: prefer Inferno before same-turn self-damage triggers.</summary>
    public static int EstimateInfernoOpenerValue(CombatState state, int infernoHandIndex) {
        if (infernoHandIndex < 0 || infernoHandIndex >= state.Hand.Count)
            return 0;

        var inferno = state.Hand[infernoHandIndex];
        if (!PlayerPowerSimulator.InstallsInferno(inferno.Profile))
            return 0;

        int install = Math.Max(inferno.Profile.InstallAmount(PlayerPowerEffectKind.InfernoRetaliate), 6);
        int retaliation = state.Buffs.InfernoRetaliation + install;
        int enemies = ThreatModel.EffectiveAoeEnemyCount(state);
        int energyAfter = state.Energy - CombatCardCost.EffectiveCost(inferno, state);

        int bestCombo = BestHandHpLossComboValue(state, infernoHandIndex, retaliation, enemies, energyAfter);
        if (bestCombo <= 0)
            bestCombo = BestPileHpLossComboValue(state, retaliation, enemies, energyAfter);

        if (bestCombo > 0)
            return bestCombo;

        return install * enemies / 4;
    }

    static bool TryFindAffordableInferno(CombatState state, out int infernoIdx, out CombatHandCard inferno) {
        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!PlayerPowerSimulator.InstallsInferno(card.Profile)) continue;
            if (!card.CanPlay || !CombatCardCost.CanAfford(card, state)) continue;
            infernoIdx = i;
            inferno = card;
            return true;
        }

        infernoIdx = -1;
        inferno = null!;
        return false;
    }

    static bool HasAffordableHpLossPartner(
        CombatState state,
        int infernoIdx,
        int energyAfter,
        bool handOnly) {
        for (int i = 0; i < state.Hand.Count; i++) {
            if (i == infernoIdx) continue;
            var card = state.Hand[i];
            if (card.Profile.HpLoss <= 0) continue;
            if (!card.CanPlay || card.Cost > energyAfter) continue;
            return true;
        }

        if (handOnly)
            return false;

        foreach (var pileCard in DrawPlanner.PeekTop(state, DrawPlanner.DefaultPeekCount)) {
            if (!CardMechanicIndex.TryGet(pileCard.Id, out var profile) || profile.HpLoss <= 0)
                continue;
            if (CombatDamageCalc.PlanningCost(pileCard, state.Modifiers, energyAfter) > energyAfter)
                continue;
            return true;
        }

        foreach (var pileCard in state.DiscardPile) {
            if (!CardMechanicIndex.TryGet(pileCard.Id, out var profile) || profile.HpLoss <= 0)
                continue;
            if (CombatDamageCalc.PlanningCost(pileCard, state.Modifiers, energyAfter) > energyAfter)
                continue;
            return true;
        }

        return false;
    }

    static int BestHandHpLossComboValue(
        CombatState state,
        int infernoIdx,
        int retaliation,
        int enemies,
        int energyAfter) {
        int bestCombo = 0;
        for (int i = 0; i < state.Hand.Count; i++) {
            if (i == infernoIdx) continue;
            var card = state.Hand[i];
            if (card.Profile.HpLoss <= 0) continue;
            if (!card.CanPlay || card.Cost > energyAfter) continue;
            bestCombo = Math.Max(bestCombo, HpLossComboDamage(card, state, retaliation, enemies));
        }

        return bestCombo;
    }

    static int BestPileHpLossComboValue(
        CombatState state,
        int retaliation,
        int enemies,
        int energyAfter) {
        int bestCombo = 0;

        foreach (var pileCard in DrawPlanner.PeekTop(state, DrawPlanner.DefaultPeekCount)) {
            if (!CardMechanicIndex.TryGet(pileCard.Id, out var profile) || profile.HpLoss <= 0)
                continue;
            if (CombatDamageCalc.PlanningCost(pileCard, state.Modifiers, energyAfter) > energyAfter)
                continue;

            int combo = retaliation * enemies * 3 / 5;
            if (profile.Damage is > 0)
                combo += profile.Damage.Value * 3 / 5;
            bestCombo = Math.Max(bestCombo, combo);
        }

        foreach (var pileCard in state.DiscardPile) {
            if (!CardMechanicIndex.TryGet(pileCard.Id, out var profile) || profile.HpLoss <= 0)
                continue;
            if (CombatDamageCalc.PlanningCost(pileCard, state.Modifiers, energyAfter) > energyAfter)
                continue;

            int combo = retaliation * enemies / 3;
            if (profile.Damage is > 0)
                combo += profile.Damage.Value / 3;
            bestCombo = Math.Max(bestCombo, combo);
        }

        return bestCombo;
    }

    static int HpLossComboDamage(CombatHandCard card, CombatState state, int retaliation, int enemies) {
        int combo = retaliation * enemies;
        if (card.IsAttack && card.Damage > 0) {
            var target = state.Enemies.FirstOrDefault(e => e.IsAlive);
            combo += CombatDamageCalc.OutgoingDamage(card, state, target?.Vulnerable ?? 0);
        }

        return combo;
    }

    /// <summary>Greedy sim only plays hand hp-loss; pile partners count as enemy HP reduction (CompareLines ranks this before InfernoOutlook).</summary>
    static int InfernoComboEnemyHpCredit(CombatState midTurn) {
        if (midTurn.Buffs.InfernoRetaliation <= 0)
            return 0;

        int enemies = ThreatModel.EffectiveAoeEnemyCount(midTurn);
        if (enemies < 2)
            return 0;

        if (HasAffordableHpLossPartner(midTurn, -1, midTurn.Energy, handOnly: true))
            return 0;

        if (!HasAffordableHpLossPartner(midTurn, -1, midTurn.MaxEnergy, handOnly: false))
            return 0;

        int retaliation = Math.Max(midTurn.Buffs.InfernoRetaliation, 6);
        return BestPileHpLossComboDamage(midTurn, retaliation, enemies, midTurn.MaxEnergy);
    }

    static int BestPileHpLossComboDamage(
        CombatState state,
        int retaliation,
        int enemies,
        int planningEnergy) {
        int best = 0;

        foreach (var pileCard in DrawPlanner.PeekTop(state, DrawPlanner.DefaultPeekCount)) {
            if (!CardMechanicIndex.TryGet(pileCard.Id, out var profile) || profile.HpLoss <= 0)
                continue;
            if (CombatDamageCalc.PlanningCost(pileCard, state.Modifiers, planningEnergy) > planningEnergy)
                continue;

            int combo = retaliation * enemies;
            if (profile.Damage is > 0)
                combo += profile.Damage.Value;
            best = Math.Max(best, combo);
        }

        foreach (var pileCard in state.DiscardPile) {
            if (!CardMechanicIndex.TryGet(pileCard.Id, out var profile) || profile.HpLoss <= 0)
                continue;
            if (CombatDamageCalc.PlanningCost(pileCard, state.Modifiers, planningEnergy) > planningEnergy)
                continue;

            int combo = retaliation * enemies;
            if (profile.Damage is > 0)
                combo += profile.Damage.Value;
            best = Math.Max(best, combo / 2);
        }

        return best;
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

    /// <summary>Greedy sim focus: this-turn attackers, then status injectors, then horizon threat.</summary>
    public static int GreedyAttackFocusIndex(CombatState state) {
        var rushPrimary = PrimaryWipeEngagementPolicy.RushPrimaryTarget(state);
        if (rushPrimary != null && PrimaryWipeEngagementPolicy.ShouldRushPrimary(state))
            return rushPrimary.Index;

        if (ThreatModel.IncomingDamage(state) > 0) {
            var attacker = state.Enemies
                .Where(e => e.IsAlive && e.EffectiveIncoming > 0
                    && ThreatModel.IsViableAttackTarget(state, e)
                    && PrimaryWipeEngagementPolicy.PreferMinionAttackerFocus(state, e))
                .OrderByDescending(e => e.EffectiveIncoming)
                .ThenBy(e => e.EffectiveHp)
                .FirstOrDefault();
            if (attacker != null)
                return attacker.Index;
        }

        var statusThreat = state.Enemies
            .Where(e => e.IsAlive && ThreatModel.IsViableAttackTarget(state, e))
            .Where(e => ThreatModel.NonDamageForStep(state, e, 0) > 0
                || e.MechanicFlags.HasFlag(EnemyMechanicFlags.HasStatusCardIntent))
            .OrderBy(e => e.EffectiveHp)
            .ThenByDescending(e => ThreatModel.NonDamageForStep(state, e, 0))
            .FirstOrDefault();
        if (statusThreat != null)
            return statusThreat.Index;

        return PrimaryAttackTargetIndex(state);
    }

    public static IEnumerable<CombatEnemy> OrderEnemiesForGreedyAttacks(CombatState state) {
        if (PrimaryWipeEngagementPolicy.ShouldRushPrimary(state))
            return OrderEnemiesByThreat(state);

        if (ThreatModel.IncomingDamage(state) <= 0)
            return OrderEnemiesByThreat(state);

        var attackers = state.Enemies
            .Where(e => e.IsAlive && e.EffectiveIncoming > 0
                && ThreatModel.IsViableAttackTarget(state, e)
                && PrimaryWipeEngagementPolicy.PreferMinionAttackerFocus(state, e))
            .OrderByDescending(e => e.EffectiveIncoming)
            .ThenBy(e => e.EffectiveHp);
        var rest = OrderEnemiesByThreat(state)
            .Where(e => e.EffectiveIncoming <= 0);
        return attackers.Concat(rest);
    }

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

            int cost = CombatCardCost.EffectiveCost(card, state);
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

        int focusIndex = GreedyAttackFocusIndex(state);

        while (true) {
            var focusEnemy = s.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == focusIndex);
            if (focusEnemy == null) {
                focusIndex = GreedyAttackFocusIndex(s);
                focusEnemy = s.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == focusIndex);
            }

            int incomingSlack = ThreatModel.IncomingDamage(s) > 0
                ? 0
                : focusEnemy != null ? ThreatModel.IncomingTradeSlack(focusEnemy, s) : 0;

            SimCombatAction? bestAction = null;
            int bestScore = int.MinValue;
            int bestIncoming = int.MaxValue;
            int bestFuture0 = int.MaxValue;
            int bestFuture1 = int.MaxValue;
            int bestFuture2 = int.MaxValue;
            int bestFocusHp = int.MaxValue;
            int bestNonDamage = int.MaxValue;
            int bestEnemyHp = int.MaxValue;
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
                    int nonDamage = ThreatModel.TotalNonDamageThreat(next);
                    int enemyHp = AliveEnemyHp(next);
                    if (IsBetterAttackStep(
                            ThreatModel.NetDamageAfterBlock(next),
                            future.f0, future.f1, future.f2,
                            ScoreMidTurn(next),
                            FocusHpAfter(next, primary),
                            nonDamage,
                            enemyHp,
                            hitsPrimary: false,
                            bestIncoming, bestFuture0, bestFuture1, bestFuture2,
                            bestFocusHp, bestNonDamage, bestEnemyHp, bestScore, bestHitsPrimary,
                            incomingSlack)) {
                        bestScore = ScoreMidTurn(next);
                        bestIncoming = ThreatModel.NetDamageAfterBlock(next);
                        bestFuture0 = future.f0;
                        bestFuture1 = future.f1;
                        bestFuture2 = future.f2;
                        bestFocusHp = FocusHpAfter(next, primary);
                        bestNonDamage = nonDamage;
                        bestEnemyHp = enemyHp;
                        bestHitsPrimary = false;
                        bestAction = new SimCombatAction(SimActionKind.PlayCard, i, -1);
                    }

                    continue;
                }

                foreach (var enemy in OrderEnemiesForGreedyAttacks(s)) {
                    int dmg = CombatDamageCalc.OutgoingDamage(card, s, enemy.Vulnerable);
                    if (dmg <= 0) continue;

                    var next = CombatSimulator.Apply(
                        s, new SimCombatAction(SimActionKind.PlayCard, i, enemy.Index));
                    bool hitsPrimary = enemy.Index == primary;
                    var future = FuturePressureFromMidTurn(next);
                    int focusHp = FocusHpAfter(next, primary);
                    int nonDamage = ThreatModel.TotalNonDamageThreat(next);
                    int enemyHp = AliveEnemyHp(next);
                    if (IsBetterAttackStep(
                            ThreatModel.NetDamageAfterBlock(next),
                            future.f0, future.f1, future.f2,
                            ScoreMidTurn(next),
                            focusHp,
                            nonDamage,
                            enemyHp,
                            hitsPrimary,
                            bestIncoming, bestFuture0, bestFuture1, bestFuture2,
                            bestFocusHp, bestNonDamage, bestEnemyHp, bestScore, bestHitsPrimary,
                            incomingSlack)) {
                        bestScore = ScoreMidTurn(next);
                        bestIncoming = ThreatModel.NetDamageAfterBlock(next);
                        bestFuture0 = future.f0;
                        bestFuture1 = future.f1;
                        bestFuture2 = future.f2;
                        bestFocusHp = focusHp;
                        bestNonDamage = nonDamage;
                        bestEnemyHp = enemyHp;
                        bestHitsPrimary = hitsPrimary;
                        bestAction = new SimCombatAction(SimActionKind.PlayCard, i, enemy.Index);
                    }
                }
            }

            if (bestAction == null)
                break;

            s = CombatSimulator.Apply(s, bestAction);
        }

        return s;
    }

    static (int f0, int f1, int f2) FuturePressureFromMidTurn(CombatState state) {
        var afterPhase = CombatTurnResolver.ProjectAfterEnemyPhase(state);
        return (
            ThreatModel.PressureAtIntentStepKillAdjusted(afterPhase, 0),
            ThreatModel.PressureAtIntentStep(afterPhase, 1),
            ThreatModel.PressureAtIntentStep(afterPhase, 2));
    }

    static int AliveEnemyHp(CombatState state) =>
        state.Enemies.Where(e => e.IsAlive).Sum(e => e.EffectiveHp);

    static bool IsBetterAttackStep(
        int incoming,
        int future0,
        int future1,
        int future2,
        int score,
        int focusHp,
        int nonDamageThreat,
        int enemyHp,
        bool hitsPrimary,
        int bestIncoming,
        int bestFuture0,
        int bestFuture1,
        int bestFuture2,
        int bestFocusHp,
        int bestNonDamage,
        int bestEnemyHp,
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

        int futureCmp = ThreatModel.CompareFutureIncoming(
            future0, future1, future2,
            bestFuture0, bestFuture1, bestFuture2);
        if (futureCmp != 0)
            return futureCmp > 0;

        if (focusHp != bestFocusHp)
            return focusHp < bestFocusHp;

        if (enemyHp != bestEnemyHp)
            return enemyHp < bestEnemyHp;

        if (nonDamageThreat != bestNonDamage)
            return nonDamageThreat < bestNonDamage;

        if (hitsPrimary != bestHitsPrimary)
            return hitsPrimary;

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
