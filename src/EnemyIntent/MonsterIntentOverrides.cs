using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace DevMode.EnemyIntent;

/// <summary>Per-enemy, per-turn-index move overrides for dev intent editing.</summary>
internal static class MonsterIntentOverrides {
    private static readonly Dictionary<string, Dictionary<int, string>> _overrides = new(StringComparer.Ordinal);
    private static bool _initialized;

    internal static void Initialize() {
        if (_initialized)
            return;
        _initialized = true;

        CombatManager.Instance.CombatEnded += _ => ClearAll();
        CombatManager.Instance.TurnStarted += OnTurnStarted;
    }

    internal static bool HasOverride(string enemyKey, int turnIndex) =>
        _overrides.TryGetValue(enemyKey, out var turns) && turns.ContainsKey(turnIndex);

    internal static void Set(Creature enemy, int turnIndex, string moveId) {
        string key = BuildEnemyKey(enemy);
        if (!_overrides.TryGetValue(key, out Dictionary<int, string>? turns)) {
            turns = new Dictionary<int, string>();
            _overrides[key] = turns;
        }
        turns[turnIndex] = moveId;
    }

    internal static void Apply(MonsterIntentEntry entry, List<MonsterIntentStep> steps) {
        if (!_overrides.TryGetValue(entry.EnemyKey, out Dictionary<int, string>? turns))
            return;
        if (entry.Owner.Monster is not { } monster)
            return;

        foreach ((int turnIndex, string moveId) in turns.ToList()) {
            if (turnIndex < 0 || turnIndex >= steps.Count)
                continue;
            if (!MonsterIntentEditor.TryFindMoveState(monster, moveId, out MoveState? move) || move == null)
                continue;

            steps[turnIndex] = MonsterIntentReader.BuildStepFromMove(
                monster,
                move,
                turnIndex,
                isUncertain: false);
        }
    }

    internal static void ClearAll() => _overrides.Clear();

    private static void OnTurnStarted(CombatState state) {
        if (state.CurrentSide != CombatSide.Player)
            return;
        Callable.From(() => ShiftAndApply(state)).CallDeferred();
    }

    private static void ShiftAndApply(CombatState state) {
        if (!CombatManager.Instance.IsInProgress)
            return;

        foreach (string key in _overrides.Keys.ToList()) {
            if (!_overrides.TryGetValue(key, out Dictionary<int, string>? turns))
                continue;

            var shifted = new Dictionary<int, string>();
            foreach ((int index, string moveId) in turns) {
                if (index <= 0)
                    continue;
                shifted[index - 1] = moveId;
            }

            if (shifted.Count == 0)
                _overrides.Remove(key);
            else
                _overrides[key] = shifted;
        }

        foreach (Creature enemy in state.HittableEnemies) {
            if (!enemy.IsAlive || enemy.Monster is not { } monster)
                continue;

            string key = BuildEnemyKey(enemy);
            if (!_overrides.TryGetValue(key, out Dictionary<int, string>? turns))
                continue;
            if (!turns.TryGetValue(0, out string? moveId))
                continue;

            if (MonsterIntentEditor.TryFindMoveState(monster, moveId, out MoveState? move) && move != null) {
                try {
                    monster.SetMoveImmediate(move, forceTransition: true);
                }
                catch {
                    // Ignore apply failures during auto-shift.
                }
            }

            turns.Remove(0);
            if (turns.Count == 0)
                _overrides.Remove(key);
        }

        MonsterIntentOverlayTracker.NotifyChanged();
    }

    internal static string BuildEnemyKey(Creature enemy) {
        if (enemy.Monster is not { } monster)
            return enemy.GetHashCode().ToString();
        string slot = enemy.SlotName ?? enemy.GetHashCode().ToString();
        return $"{monster.Id.Entry}:{slot}";
    }
}
