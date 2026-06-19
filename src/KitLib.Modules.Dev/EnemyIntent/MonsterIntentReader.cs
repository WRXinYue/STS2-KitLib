using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Random;

namespace KitLib.EnemyIntent;

internal sealed record MonsterIntentStep(
    string MoveId,
    string MoveName,
    IReadOnlyList<AbstractIntent> Intents,
    bool IsCurrent,
    bool IsUncertain);

internal sealed record MonsterIntentEntry(
    string EnemyKey,
    string DisplayName,
    Creature Owner,
    IReadOnlyList<Creature> Targets,
    IReadOnlyList<MonsterIntentStep> Steps);

internal static class MonsterIntentReader {
    private const int MaxPredictedTurns = 12;
    private static Rng? _dummyRng;
    private static Rng DummyRng => _dummyRng ??= new Rng(0);

    internal static bool IsOverlayCombatReady(CombatState? state) {
        if (state == null || !CombatManager.Instance.IsInProgress)
            return false;
        if (LocalContext.GetMe(state.Players) == null)
            return false;
        foreach (Creature player in state.PlayerCreatures) {
            if (player.IsAlive)
                return true;
        }
        return false;
    }

    public static IReadOnlyList<MonsterIntentEntry> CaptureCurrent(CombatState? state) {
        if (!IsOverlayCombatReady(state))
            return Array.Empty<MonsterIntentEntry>();

        var targets = state!.PlayerCreatures;
        var entries = new List<MonsterIntentEntry>();

        foreach (Creature enemy in state.HittableEnemies) {
            if (!enemy.IsAlive || enemy.Monster is not { } monster)
                continue;

            entries.Add(new MonsterIntentEntry(
                MonsterIntentOverrides.BuildEnemyKey(enemy),
                monster.Title.GetFormattedText(),
                enemy,
                targets,
                PredictSteps(monster, enemy)));
        }

        return entries;
    }

    /// <summary>Intent chain for AI snapshot (current + up to 3 predicted enemy turns).</summary>
    internal static JsonArray CaptureIntentSteps(
        Creature enemy,
        IReadOnlyList<Creature> targets,
        KitLib.AI.Combat.Simulation.CombatState pressureState,
        int maxSteps = KitLib.AI.Combat.Simulation.ThreatModel.LineFutureHorizonTurns + 2) {
        var arr = new JsonArray();
        if (enemy.Monster is not { } monster)
            return arr;

        var steps = PredictSteps(monster, enemy);
        for (int i = 0; i < Math.Min(maxSteps, steps.Count); i++) {
            var step = steps[i];
            int damage = 0;
            foreach (var intent in step.Intents) {
                if (intent is AttackIntent attack) {
                    try {
                        damage += attack.GetTotalDamage(targets, enemy);
                    }
                    catch { }
                }
            }

            var intentTypes = new JsonArray();
            foreach (var intent in step.Intents)
                intentTypes.Add(intent.IntentType.ToString());

            string? monsterId = null;
            try { monsterId = enemy.ModelId.Entry; } catch { }
            var effects = MoveEffectIndex.MergeWithRuntimeIntents(monsterId, step.MoveId, step.Intents);
            int nonDamage = KitLib.AI.Combat.Simulation.MoveEffectPressure.FromEffects(
                pressureState,
                monsterId,
                step.MoveId,
                effects);

            arr.Add(new JsonObject {
                ["moveId"] = step.MoveId,
                ["intentDamage"] = damage,
                ["isUncertain"] = step.IsUncertain,
                ["intentTypes"] = intentTypes,
                ["nonDamageThreat"] = nonDamage,
            });
        }

        return arr;
    }

    public static IReadOnlyList<MonsterIntentEntry> CaptureNextTurn(CombatState? state) {
        if (!IsOverlayCombatReady(state))
            return Array.Empty<MonsterIntentEntry>();

        var targets = state!.PlayerCreatures;
        var entries = new List<MonsterIntentEntry>();

        foreach (Creature enemy in state.HittableEnemies) {
            if (!enemy.IsAlive || enemy.Monster is not { } monster)
                continue;

            List<MonsterIntentStep> steps = PredictSteps(monster, enemy);
            if (steps.Count < 2)
                continue;

            MonsterIntentStep nextStep = steps[1];
            if (nextStep.Intents.Count == 0)
                continue;

            entries.Add(new MonsterIntentEntry(
                MonsterIntentOverrides.BuildEnemyKey(enemy),
                monster.Title.GetFormattedText(),
                enemy,
                targets,
                new[] { nextStep }));
        }

        return entries;
    }

    private static List<MonsterIntentStep> PredictSteps(
        MonsterModel monster,
        Creature enemy) {
        var steps = new List<MonsterIntentStep>(MaxPredictedTurns);

        MoveState? current = monster.NextMove;
        bool uncertain = false;

        for (int turn = 0; turn < MaxPredictedTurns && current != null; turn++) {
            string moveId = string.IsNullOrWhiteSpace(current.StateId) ? current.Id : current.StateId;

            steps.Add(new MonsterIntentStep(
                moveId,
                ResolveMoveName(monster, moveId),
                CollectVisibleIntents(current),
                IsCurrent: turn == 0,
                IsUncertain: turn > 0 && uncertain));

            current = PredictNextMove(monster, enemy, current, out uncertain);
        }

        MonsterIntentOverrides.Apply(
            new MonsterIntentEntry(
                MonsterIntentOverrides.BuildEnemyKey(enemy),
                monster.Title.GetFormattedText(),
                enemy,
                Array.Empty<Creature>(),
                steps),
            steps);

        return steps;
    }

    internal static MonsterIntentStep BuildStepFromMove(
        MonsterModel monster,
        MoveState move,
        int turnIndex,
        bool isUncertain) {
        string moveId = string.IsNullOrWhiteSpace(move.StateId) ? move.Id : move.StateId;
        return new MonsterIntentStep(
            moveId,
            ResolveMoveName(monster, moveId),
            CollectVisibleIntents(move),
            IsCurrent: turnIndex == 0,
            IsUncertain: isUncertain);
    }

    private static IReadOnlyList<AbstractIntent> CollectVisibleIntents(MoveState move) =>
        move.Intents
            .Where(i => i.IntentType != IntentType.Hidden)
            .ToList();

    private static MoveState? PredictNextMove(
        MonsterModel monster,
        Creature owner,
        MoveState currentMove,
        out bool uncertain) {
        uncertain = false;
        if (monster.MoveStateMachine is not { } machine)
            return null;

        try {
            MonsterState state = WalkToNextMoveState(
                machine,
                currentMove,
                owner,
                ref uncertain);
            return state as MoveState;
        }
        catch {
            return null;
        }
    }

    private static MonsterState WalkToNextMoveState(
        MonsterMoveStateMachine machine,
        MoveState fromMove,
        Creature owner,
        ref bool uncertain) {
        string nextId = fromMove.GetNextState(owner, DummyRng);
        if (string.IsNullOrEmpty(nextId) || !machine.States.TryGetValue(nextId, out MonsterState? state))
            throw new InvalidOperationException("Missing follow-up state.");

        while (!state.IsMove) {
            (nextId, bool branchUncertain) = PredictNextStateId(state, owner, machine);
            uncertain |= branchUncertain;
            if (string.IsNullOrEmpty(nextId) || !machine.States.TryGetValue(nextId, out state))
                throw new InvalidOperationException("Missing branch state.");
        }

        return state;
    }

    private static (string nextId, bool uncertain) PredictNextStateId(
        MonsterState state,
        Creature owner,
        MonsterMoveStateMachine machine) {
        if (state is RandomBranchState random)
            return PredictRandomBranch(random, owner, machine);

        return (state.GetNextState(owner, DummyRng), false);
    }

    private static (string nextId, bool uncertain) PredictRandomBranch(
        RandomBranchState random,
        Creature owner,
        MonsterMoveStateMachine machine) {
        var weighted = random.States
            .Select(sw => (sw, weight: CalcBranchWeight(sw, owner, machine)))
            .Where(x => x.weight > 0f)
            .OrderByDescending(x => x.weight)
            .ToList();

        if (weighted.Count == 0)
            return ("", true);

        bool uncertain = weighted.Count > 1;
        return (weighted[0].sw.stateId, uncertain);
    }

    private static float CalcBranchWeight(
        RandomBranchState.StateWeight stateWeight,
        Creature owner,
        MonsterMoveStateMachine machine) {
        float multiplier = 1f;
        if (stateWeight.repeatType.Equals(MoveRepeatType.UseOnlyOnce)) {
            MonsterState item = machine.States[stateWeight.stateId];
            if (machine.StateLog.Contains(item))
                multiplier = 0f;
        }
        else if (!stateWeight.repeatType.Equals(MoveRepeatType.CanRepeatForever)) {
            float cap = stateWeight.repeatType.Equals(MoveRepeatType.CannotRepeat)
                ? 1f
                : stateWeight.maxTimes;
            multiplier = machine.StateLog.Count < cap ? 1f : 0f;
            int streak = 0;
            while (machine.StateLog.Count >= cap && streak < cap && machine.StateLog.Count - streak > 0) {
                MonsterState move = machine.States[stateWeight.stateId];
                if (machine.StateLog[machine.StateLog.Count - 1 - streak] != move) {
                    multiplier = 1f;
                    break;
                }
                streak++;
            }
        }

        if (stateWeight.cooldown > 0) {
            IEnumerable<MonsterState> recent = machine.StateLog
                .Where(s => s.IsMove)
                .Reverse()
                .Take(stateWeight.cooldown);
            if (recent.Any(move => move.Id == stateWeight.stateId))
                return 0f;
        }

        return multiplier * stateWeight.GetWeight();
    }

    internal static string ResolveMoveName(MonsterModel monster, string moveId) {
        return TryResolveMoveLoc(monster, moveId)
            ?? FormatMoveIdFallback(moveId);
    }

    internal static string? TryResolveMoveLoc(MonsterModel monster, string moveId) {
        if (string.IsNullOrWhiteSpace(moveId))
            return null;

        try {
            var loc = MonsterModel.L10NMonsterLookup($"{monster.Id.Entry}.moves.{moveId}.title");
            string text = BbcodeTextHelper.ToPlainTooltipText(loc.GetFormattedText());
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string missingKey = $"{monster.Id.Entry}.moves.{moveId}.title";
            if (string.Equals(text, missingKey, StringComparison.Ordinal))
                return null;

            return text;
        }
        catch {
            return null;
        }
    }

    internal static string FormatMoveIdFallback(string moveId) {
        if (string.IsNullOrWhiteSpace(moveId))
            return "—";

        if (!moveId.Contains('_', StringComparison.Ordinal))
            return moveId;

        return moveId.Replace('_', ' ');
    }
}
