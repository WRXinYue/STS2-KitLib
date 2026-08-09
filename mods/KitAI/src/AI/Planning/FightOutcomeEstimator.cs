using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat;
using KitLib.AI.Combat.Simulation;

namespace KitLib.AI.Planning;

public sealed record FightOutcomeMetrics(
    int ExpectedRemainingHp,
    int MinRemainingHp,
    int MaxRemainingHp,
    int ExpectedChip,
    int MaxChip,
    int ExpectedKillTurns,
    int ExpectedFightChip,
    int LethalSamples,
    int SampleCount);

/// <summary>
/// Monte Carlo turn-1 fight outcome: enemy remaining HP and player chip after coupled attack/block energy split.
/// </summary>
public static class FightOutcomeEstimator {
    public const int UnkillableTurns = 99;

    public static FightOutcomeMetrics EstimateAverage(
        JsonObject snapshot,
        JsonObject? offeredCard,
        NextFightNode fight,
        int sampleCount = DeckDrawEvEstimator.DefaultSampleCount) {
        if (sampleCount <= 0)
            sampleCount = DeckDrawEvEstimator.DefaultSampleCount;

        long remainingTotal = 0;
        int minRemaining = int.MaxValue;
        int maxRemaining = 0;
        long chipTotal = 0;
        int maxChip = 0;
        long killTurnsTotal = 0;
        long fightChipTotal = 0;
        int lethalSamples = 0;

        for (int s = 0; s < sampleCount; s++) {
            var state = DeckCombatStateFactory.BuildOpeningTurn(
                snapshot, offeredCard, fight.Enemies, s);
            var sample = EstimateSingle(state, fight);

            remainingTotal += sample.RemainingHp;
            minRemaining = Math.Min(minRemaining, sample.RemainingHp);
            maxRemaining = Math.Max(maxRemaining, sample.RemainingHp);
            chipTotal += sample.Chip;
            maxChip = Math.Max(maxChip, sample.Chip);
            killTurnsTotal += sample.KillTurns;
            fightChipTotal += sample.FightChip;
            if (sample.RemainingHp == 0)
                lethalSamples++;
        }

        int n = sampleCount;
        return new FightOutcomeMetrics(
            (int)(remainingTotal / n),
            minRemaining == int.MaxValue ? 0 : minRemaining,
            maxRemaining,
            (int)(chipTotal / n),
            maxChip,
            (int)(killTurnsTotal / n),
            (int)(fightChipTotal / n),
            lethalSamples,
            n);
    }

    public static FightOutcomeSample EstimateSingle(CombatState state, NextFightNode fight) {
        int incoming = fight.IncomingTurn1;
        int enemyHp = PrimaryEffectiveHp(fight);
        if (enemyHp <= 0)
            return new FightOutcomeSample(0, 0, 0, 0);

        int targetIndex = PrimaryTargetIndex(fight);
        int energy = state.Energy;

        int bestRemaining = enemyHp;
        int bestChip = incoming;
        int bestDamage = 0;

        for (int eDef = 0; eDef <= energy; eDef++) {
            int block = BlockDefensePolicy.AffordableBlockWithBudget(state, eDef);
            int chip = Math.Max(0, incoming - block - state.PlayerBlock);
            int eAtk = energy - eDef;
            int damage = eAtk > 0
                ? LethalDamageSolver.MaxSingleTargetDamage(state, targetIndex, eAtk)
                : 0;

            if (damage >= LethalDamageSolver.AoeWipeSignal) {
                bestRemaining = 0;
                bestChip = chip;
                bestDamage = damage;
                break;
            }

            int remaining = Math.Max(0, enemyHp - damage);
            if (remaining < bestRemaining || (remaining == bestRemaining && chip < bestChip)) {
                bestRemaining = remaining;
                bestChip = chip;
                bestDamage = damage;
            }
        }

        int killTurns = EstimateKillTurns(enemyHp, bestDamage);
        int fightChip = killTurns >= UnkillableTurns
            ? bestChip * 8
            : bestChip * killTurns;

        return new FightOutcomeSample(bestRemaining, bestChip, killTurns, fightChip);
    }

    public static int ScoreOutcome(FightOutcomeMetrics outcome, JsonObject snapshot, NextFightNode fight) {
        int penalty = 0;
        penalty -= Math.Min(outcome.ExpectedRemainingHp / 3, 45);
        if (outcome.MinRemainingHp > 40)
            penalty -= 12;
        if (outcome.MaxRemainingHp > 60)
            penalty -= 8;

        if (outcome.ExpectedKillTurns >= 5)
            penalty -= 22;
        if (outcome.ExpectedKillTurns >= 6)
            penalty -= 38;
        if (outcome.ExpectedKillTurns >= UnkillableTurns / 2)
            penalty -= 50;

        penalty -= Math.Min(outcome.ExpectedChip * 4, 35);
        penalty -= Math.Min(outcome.ExpectedFightChip / 5, 55);

        var playerHp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        if (outcome.MaxChip >= playerHp && playerHp > 0)
            penalty -= 45;
        if (outcome.ExpectedFightChip >= playerHp && playerHp > 0)
            penalty -= 30;

        if (outcome.LethalSamples > 0 && outcome.SampleCount > 0) {
            float lethalRate = (float)outcome.LethalSamples / outcome.SampleCount;
            penalty += (int)Math.Round(lethalRate * 20f);
        }

        if (fight.Enemies.Any(e =>
                string.Equals(e.MonsterId, "BYRDONIS", StringComparison.OrdinalIgnoreCase))) {
            if (outcome.ExpectedKillTurns >= 5)
                penalty -= 18;
            if (outcome.ExpectedFightChip > 20)
                penalty -= Math.Min(outcome.ExpectedFightChip / 4, 25);
        }

        return penalty;
    }

    static int PrimaryTargetIndex(NextFightNode fight) {
        var targets = fight.Enemies.Where(e => e.IsAlive && !e.IsMinion).ToList();
        if (targets.Count == 0) {
            var any = fight.Enemies.FirstOrDefault(e => e.IsAlive);
            return any?.Index ?? 0;
        }

        return targets.OrderByDescending(e => e.EffectiveHp).First().Index;
    }

    static int PrimaryEffectiveHp(NextFightNode fight) =>
        fight.Enemies.Where(e => e.IsAlive && !e.IsMinion).Sum(e => e.EffectiveHp);

    static int EstimateKillTurns(int enemyHp, int turn1Damage) {
        if (enemyHp <= 0)
            return 0;
        if (turn1Damage <= 0)
            return UnkillableTurns;
        if (turn1Damage >= LethalDamageSolver.AoeWipeSignal)
            return 1;
        return (enemyHp + turn1Damage - 1) / turn1Damage;
    }

    public readonly record struct FightOutcomeSample(
        int RemainingHp,
        int Chip,
        int KillTurns,
        int FightChip);
}
