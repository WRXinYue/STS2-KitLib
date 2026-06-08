using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

public static class ThreatModel {
    /// <summary>Future enemy rounds compared in line outcome (after the current line resolves).</summary>
    public const int LineFutureHorizonTurns = 3;

    public static int FocusHp(CombatState state, int focusIndex) {
        var focus = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == focusIndex);
        return focus?.CurrentHp ?? 0;
    }

    /// <summary>Vulnerable stacks on the current focus target for offensive EV estimates.</summary>
    public static int FocusVulnerableStacks(CombatState state) {
        int focusIndex = CombatSetupEvaluator.PrimaryAttackTargetIndex(state);
        var focus = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == focusIndex);
        return focus?.Vulnerable ?? 0;
    }

    /// <summary>Mid-turn greedy tie-break aligned with <see cref="CombatSetupEvaluator.CompareLines"/>.</summary>
    public static int MidTurnScore(CombatState state, int focusIndex) {
        var afterPhase = CombatTurnResolver.ProjectAfterEnemyPhase(state);
        return CombatSetupEvaluator.PackLineScore(new CombatSetupEvaluator.CombatLineOutcome(
            NetDamageAfterBlock(state),
            PressureAtIntentStepKillAdjusted(afterPhase, 0),
            PressureAtIntentStep(afterPhase, 1),
            PressureAtIntentStep(afterPhase, 2),
            DeckPollutionEvaluator.EffectivePollutionBurden(state),
            FocusHp(state, focusIndex),
            state.Enemies.Where(e => e.IsAlive).Sum(e => e.EffectiveHp),
            VulnerableOutlookEvaluator.Estimate(state),
            WeakMitigationEvaluator.Estimate(state),
            0,
            PileRhythmEvaluator.DrawPileOutlook(state),
            state.PlayerHp,
            0));
    }

    /// <summary>Sum of mitigated attack pressure over the line-evaluation horizon.</summary>
    public static int ScheduledPressureScore(CombatState state) =>
        PressureAtIntentStepKillAdjusted(state, 0, afterDrawTurnStart: false)
        + PressureAtIntentStep(state, 1)
        + PressureAtIntentStep(state, 2);

    /// <summary>Intent attack damage before weak mitigation (for EV estimates).</summary>
    public static int RawIntentDamageAtStep(CombatEnemy enemy, int stepIndex) {
        if (!enemy.IsAlive || stepIndex < 0 || stepIndex >= enemy.IntentSteps.Length)
            return 0;

        var step = enemy.IntentSteps[stepIndex];
        return ResolveStepDamage(enemy, step, mitigateWeak: false);
    }

    /// <summary>Acceptable extra this-turn incoming when focus progress justifies it.</summary>
    public static int IncomingTradeSlack(CombatEnemy focus, CombatState state) {
        if (!focus.IsAlive)
            return 0;

        int poke = focus.IntentDamage + NonDamageForStep(state, focus, 0);
        int peak = PeakScheduledDamage(focus);
        int horizon = HorizonThreatForEnemy(focus, state, 1, LineFutureHorizonTurns);
        int perStep = horizon / Math.Max(1, LineFutureHorizonTurns);
        return Math.Max(poke, Math.Max(peak, perStep));
    }

    public static int IncomingDamage(CombatState state) =>
        state.Enemies
            .Where(e => e.IsAlive && e.EffectiveIncoming > 0)
            .Sum(e => e.EffectiveIncoming);

    public static int NetDamageAfterBlock(CombatState state) =>
        Math.Max(0, IncomingDamage(state) - state.PlayerBlock);

    public static int EffectiveHp(CombatState state) =>
        Math.Max(1, state.PlayerHp - state.StatusDamage);

    public static bool IsFatalIfUnblocked(CombatState state) =>
        NetDamageAfterBlock(state) >= EffectiveHp(state);

    public static int NextTurnIncoming(CombatState state) =>
        IncomingAtIntentStep(state, 1);

    /// <summary>Sum attack damage at intentSteps[stepIndex] across alive enemies.</summary>
    public static int IncomingAtIntentStep(CombatState state, int stepIndex) {
        if (stepIndex < 0)
            return 0;

        int total = 0;
        foreach (var enemy in state.Enemies.Where(e => e.IsAlive))
            total += IncomingAtIntentStepForEnemy(enemy, stepIndex);

        return total;
    }

    /// <summary>Non-damage pressure for one enemy intent step from the current deck/hand.</summary>
    public static int NonDamageForStep(CombatState state, CombatEnemy enemy, int stepIndex) {
        if (!enemy.IsAlive || stepIndex < 0 || stepIndex >= enemy.IntentSteps.Length)
            return 0;

        var step = enemy.IntentSteps[stepIndex];
        if (string.IsNullOrWhiteSpace(step.MoveId))
            return 0;

        return MoveEffectPressure.FromMove(state, enemy.MonsterId, step.MoveId);
    }

    /// <summary>Sum non-damage pressure at intentSteps[stepIndex] across alive enemies.</summary>
    public static int NonDamageAtIntentStep(CombatState state, int stepIndex) {
        if (stepIndex < 0)
            return 0;

        int total = 0;
        foreach (var enemy in state.Enemies.Where(e => e.IsAlive)) {
            if (stepIndex >= enemy.IntentSteps.Length)
                continue;
            total += NonDamageForStep(state, enemy, stepIndex);
        }

        return total;
    }

    /// <summary>Damage + debuff pressure at one future enemy phase (for line horizon).</summary>
    public static int PressureAtIntentStep(CombatState state, int stepIndex) =>
        IncomingAtIntentStep(state, stepIndex) + NonDamageAtIntentStep(state, stepIndex);

    /// <summary>
    /// Scheduled pressure at <paramref name="stepIndex"/>, discounting enemies killable with
    /// projected playable damage (and block for attacks) before they act.
    /// </summary>
    public static int PressureAtIntentStepKillAdjusted(CombatState state, int stepIndex, bool afterDrawTurnStart = false) {
        if (stepIndex != 0)
            return PressureAtIntentStep(state, stepIndex);

        int budget = PlayableDamageBeforeIntentStep(state, afterDrawTurnStart);
        var (attack, nonDamage) = ScheduledPressureAfterKillBudget(state, stepIndex, budget);
        int block = PlayableBlockBeforeIntentStep(state, afterDrawTurnStart);
        return Math.Max(0, attack - Math.Min(block, attack)) + nonDamage;
    }

    /// <summary>Attack damage playable before enemies execute intentSteps[0].</summary>
    public static int PlayableDamageBeforeIntentStep(CombatState state, bool afterDrawTurnStart) {
        if (afterDrawTurnStart)
            return DeckPollutionEvaluator.ExpectedPlayableDamage(state);

        int vuln = FocusVulnerableStacks(state);
        return EstimateRemainingTurnDamage(state, vuln)
            + DrawPlanner.ExpectedDrawnDamage(
                state,
                RelicCombatRules.PlannedHandDraw(state),
                state.MaxEnergy,
                vuln);
    }

    /// <summary>Block playable before enemies execute intentSteps[0].</summary>
    public static int PlayableBlockBeforeIntentStep(CombatState state, bool afterDrawTurnStart) {
        if (afterDrawTurnStart)
            return DeckPollutionEvaluator.ExpectedPlayableBlock(state);

        return EstimateRemainingTurnBlock(state)
            + DrawPlanner.ExpectedDrawnBlock(
                state,
                RelicCombatRules.PlannedHandDraw(state),
                state.MaxEnergy);
    }

    public static int EstimateRemainingTurnDamage(CombatState state, int vulnerableOnFocus = -1) {
        int vuln = vulnerableOnFocus >= 0 ? vulnerableOnFocus : FocusVulnerableStacks(state);
        int total = 0;
        int energy = state.Energy;
        foreach (var card in state.Hand.OrderByDescending(c => c.Damage)) {
            if (!CombatCardCost.CanAfford(card, state))
                continue;
            if (!card.IsAttack || card.Damage <= 0)
                continue;

            int cost = CombatCardCost.EffectiveCost(card, state);
            if (cost > energy)
                continue;

            energy -= cost;
            total += CombatDamageCalc.OutgoingDamage(card, state, vuln);
        }

        return total;
    }

    public static int EstimateRemainingTurnBlock(CombatState state) {
        int total = 0;
        int energy = state.Energy;
        foreach (var card in state.Hand.OrderByDescending(c => c.Block)) {
            if (!CombatCardCost.CanAfford(card, state))
                continue;

            int block = CombatDamageCalc.OutgoingBlock(card, state);
            if (block <= 0)
                continue;

            int cost = CombatCardCost.EffectiveCost(card, state);
            if (cost > energy)
                continue;

            energy -= cost;
            total += block;
        }

        return total;
    }

    static (int Attack, int NonDamage) ScheduledPressureAfterKillBudget(
        CombatState state,
        int stepIndex,
        int damageBudget) {
        int budget = Math.Max(0, damageBudget);
        int attack = 0;
        int nonDamage = 0;

        foreach (var enemy in OrderEnemiesForKillBeforeStep(state, stepIndex)) {
            int inc = IncomingAtIntentStepForEnemy(enemy, stepIndex);
            int nd = NonDamageForStep(state, enemy, stepIndex);
            if (inc <= 0 && nd <= 0)
                continue;

            if (budget >= enemy.EffectiveHp) {
                budget -= enemy.EffectiveHp;
                continue;
            }

            attack += inc;
            nonDamage += nd;
        }

        return (attack, nonDamage);
    }

    static IEnumerable<CombatEnemy> OrderEnemiesForKillBeforeStep(CombatState state, int stepIndex) =>
        state.Enemies
            .Where(e => e.IsAlive)
            .OrderByDescending(e =>
                IncomingAtIntentStepForEnemy(e, stepIndex) + NonDamageForStep(state, e, stepIndex))
            .ThenBy(e => e.EffectiveHp);

    /// <summary>Intent-chain threat for focus fire over the next enemy phases.</summary>
    public static int HorizonThreatForEnemy(
        CombatEnemy enemy,
        CombatState state,
        int startStep = 0,
        int stepCount = 3) {
        if (!enemy.IsAlive || startStep < 0)
            return 0;

        int total = 0;
        for (int i = startStep; i < startStep + stepCount && i < enemy.IntentSteps.Length; i++) {
            var step = enemy.IntentSteps[i];
            int damage = BaseStepDamage(enemy, step);
            if (i > startStep && step.IsUncertain)
                damage = (int)Math.Round(damage * EnemyThreatWeights.NextTurnUncertainMultiplier);
            damage = DebuffDamageCalc.MitigateWeakIncoming(damage, enemy.Weak);
            total += damage + NonDamageForStep(state, enemy, i);
        }

        return total;
    }

    /// <summary>Focus-fire priority relative to the alive enemy threat pool.</summary>
    public static int FocusThreatScore(CombatEnemy enemy, CombatState state) {
        if (!enemy.IsAlive)
            return 0;

        var alive = state.Enemies
            .Where(e => e.IsAlive && IsViableAttackTarget(state, e))
            .ToList();
        if (alive.Count == 0)
            return 0;

        int peak = PeakScheduledDamage(enemy);
        int horizon = HorizonThreatForEnemy(enemy, state, 1, LineFutureHorizonTurns);
        int thisTurn = DebuffDamageCalc.MitigateWeakIncoming(enemy.IntentDamage, enemy.Weak)
            + NonDamageForStep(state, enemy, 0);

        int poolPeak = 0;
        foreach (var e in alive)
            poolPeak = Math.Max(poolPeak, PeakScheduledDamage(e));
        int poolHorizon = alive.Sum(e => HorizonThreatForEnemy(e, state, 1, LineFutureHorizonTurns));
        int poolHp = alive.Where(e => !e.IsMinion).Sum(e => e.CurrentHp);
        if (poolHp <= 0)
            poolHp = alive.Sum(e => e.CurrentHp);

        int avgHp = poolHp / alive.Count;
        bool smallPoke = thisTurn > 0
            && peak <= Math.Max(thisTurn, poolPeak / Math.Max(2, alive.Count))
            && enemy.CurrentHp <= avgHp;

        int peakTerm = poolPeak > 0 ? peak * 1000 / poolPeak : peak * 100;
        int horizonTerm = poolHorizon > 0 ? horizon * 1000 / poolHorizon : horizon * 100;
        int hpTerm = poolHp > 0 ? enemy.CurrentHp * 1000 / poolHp : enemy.CurrentHp * 100;
        int pokeDenom = Math.Max(1, poolPeak + thisTurn);
        int thisTerm = smallPoke
            ? thisTurn * 250 / pokeDenom
            : thisTurn * 500 / pokeDenom;

        return peakTerm + horizonTerm + hpTerm + thisTerm;
    }

    /// <summary>Max attack damage in upcoming intent steps (includes move-profile fallback).</summary>
    public static int PeakScheduledDamage(CombatEnemy enemy, int horizon = LineFutureHorizonTurns) {
        if (!enemy.IsAlive)
            return 0;

        int peak = 0;
        for (int i = 1; i <= horizon && i < enemy.IntentSteps.Length; i++) {
            var step = enemy.IntentSteps[i];
            peak = Math.Max(peak, ResolveStepDamage(enemy, step, mitigateWeak: true));
        }

        return peak;
    }

    static int BaseStepDamage(CombatEnemy enemy, CombatIntentStep step) {
        int dmg = step.IntentDamage;
        if (dmg <= 0 && !string.IsNullOrWhiteSpace(step.MoveId)) {
            foreach (var effect in MoveEffectIndex.GetEffects(enemy.MonsterId, step.MoveId)) {
                if (effect.Kind == MonsterMoveEffectKind.Attack && effect.Damage > 0)
                    dmg = Math.Max(dmg, effect.Damage + enemy.Strength);
            }
        }

        return dmg;
    }

    static int ResolveStepDamage(CombatEnemy enemy, CombatIntentStep step, bool mitigateWeak) {
        int dmg = BaseStepDamage(enemy, step);
        if (step.IsUncertain)
            dmg = (int)Math.Round(dmg * EnemyThreatWeights.NextTurnUncertainMultiplier);

        if (mitigateWeak)
            dmg = DebuffDamageCalc.MitigateWeakIncoming(dmg, enemy.Weak);
        return dmg;
    }

    /// <summary>Positive when horizon A is better (lower incoming) than horizon B.</summary>
    public static int CompareFutureIncoming(
        int a0, int a1, int a2,
        int b0, int b1, int b2) {
        if (a0 != b0) return b0 - a0;
        if (a1 != b1) return b1 - a1;
        if (a2 != b2) return b2 - a2;
        return 0;
    }

    public static int TotalNonDamageThreat(CombatState state) =>
        NonDamageAtIntentStep(state, 0);

    /// <summary>Sum of raw attack damage across the line-evaluation intent horizon.</summary>
    public static int ScheduledAttackPressure(CombatState state) =>
        IncomingAtIntentStep(state, 0)
        + IncomingAtIntentStep(state, 1)
        + IncomingAtIntentStep(state, 2);

    public static int NextTurnAttackOn(CombatEnemy enemy) =>
        IncomingAtIntentStepForEnemy(enemy, 1);

    public static int NextTurnPressureOn(CombatEnemy enemy, CombatState state) {
        if (!enemy.IsAlive)
            return 0;

        int total = 0;
        for (int i = 1; i <= LineFutureHorizonTurns && i < enemy.IntentSteps.Length; i++) {
            var step = enemy.IntentSteps[i];
            total += ResolveStepDamage(enemy, step, mitigateWeak: true)
                + NonDamageForStep(state, enemy, i);
        }

        return total;
    }

    static int IncomingAtIntentStepForEnemy(CombatEnemy enemy, int stepIndex) {
        if (!enemy.IsAlive || stepIndex < 0 || stepIndex >= enemy.IntentSteps.Length)
            return 0;

        var step = enemy.IntentSteps[stepIndex];
        return ResolveStepDamage(enemy, step, mitigateWeak: true);
    }

    /// <summary>Next-turn attack weight — full when safe this turn so kill-before-hit is valued.</summary>
    public static int ScaledNextTurnPressure(CombatState state) {
        var next = NextTurnIncoming(state);
        return IncomingDamage(state) > 0 ? next / 2 : next;
    }

    /// <summary>Card-stuff/debuff pressure — prefers deck EV model when piles are available.</summary>
    public static int ScaledNonDamagePressure(CombatState state) =>
        ThreatEconomy.ScaledNonDamagePressure(state);

    public static bool IsViableAttackTarget(CombatState state, CombatEnemy enemy) {
        if (!enemy.IsAlive)
            return false;
        if (!enemy.MechanicFlags.HasFlag(EnemyMechanicFlags.HasIllusionRevive))
            return true;

        return !state.Enemies.Any(e =>
            e.IsAlive && !e.MechanicFlags.HasFlag(EnemyMechanicFlags.HasIllusionRevive));
    }

    public static int AliveThreatCount(CombatState state) =>
        state.Enemies.Count(e => e.IsAlive && e.IntentDamage > 0);

    public static bool CanEliminateAllThreats(CombatState state, int maxSingleTargetDamage) {
        var threats = state.Enemies
            .Where(e => e.IsAlive && e.IntentDamage > 0)
            .ToList();
        if (threats.Count == 0) return true;

        foreach (var threat in threats) {
            if (threat.EffectiveHp > maxSingleTargetDamage)
                return false;
        }

        return NetDamageAfterBlock(state) <= 8;
    }

    /// <summary>Alive enemies plus minions that spawn when a living primary dies this turn.</summary>
    public static int EffectiveAoeEnemyCount(CombatState state) =>
        Math.Max(1, state.AliveEnemyCount + ImpendingDeathSpawnCount(state));

    public static int ImpendingDeathSpawnCount(CombatState state) {
        int total = 0;

        foreach (var enemy in state.Enemies.Where(e => e.IsAlive && !e.IsMinion)) {
            if (!MonsterMechanicIndex.TryGet(enemy.MonsterId, out var profile))
                continue;
            if (!profile.Flags.HasFlag(EnemyMechanicFlags.SpawnsOnDeath))
                continue;

            foreach (var spawnId in profile.SpawnedMonsterIds) {
                if (state.Enemies.Any(e => e.IsAlive
                        && string.Equals(e.MonsterId, spawnId, StringComparison.OrdinalIgnoreCase)))
                    continue;

                total += MonsterProbeOverrides.GetDeathSpawnCount(enemy.MonsterId);
            }
        }

        return total;
    }

    public static void OnPrimaryEnemyKilled(IList<CombatEnemy> enemies, int killedIndex) {
        if (killedIndex < 0 || killedIndex >= enemies.Count) return;
        var killed = enemies[killedIndex];
        if (killed.IsMinion) return;

        CombatDeathSpawnSimulator.TrySpawnOnDeath(enemies, killed);

        if (!MinionEngagementPolicy.ShouldSimulateMinionWipe(killed, enemies.ToArray()))
            return;

        for (int i = 0; i < enemies.Count; i++) {
            if (!enemies[i].IsAlive || !enemies[i].IsMinion) continue;
            if (enemies[i].MechanicFlags.HasFlag(EnemyMechanicFlags.HasIllusionRevive))
                continue;
            enemies[i] = enemies[i].MarkDead();
        }
    }

    public static int NonDamageThreatFromJson(JsonObject? enemy) =>
        EnemyMechanicResolver.ResolveNonDamageThreat(enemy);

    // JsonObject bridge for IntentCalculator
    public static int IncomingDamage(JsonObject snapshot) =>
        IncomingDamage(CombatState.FromSnapshot(snapshot));

    public static int NetDamageAfterBlock(JsonObject snapshot) =>
        NetDamageAfterBlock(CombatState.FromSnapshot(snapshot));
}
