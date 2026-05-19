using System;
using System.Collections.Generic;
using System.Linq;
using DevMode.UI;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace DevMode.EnemyIntent;

internal static class MonsterIntentEditor {
    internal sealed record MoveOption(string Id, string DisplayName);

    internal static IReadOnlyList<MoveOption> ListMoves(MonsterModel monster, Creature owner) {
        if (monster.MoveStateMachine?.States == null)
            return Array.Empty<MoveOption>();

        var options = new List<MoveOption>();
        foreach (MonsterState state in monster.MoveStateMachine.States.Values) {
            if (state is not MoveState move)
                continue;
            if (string.Equals(move.StateId, "UNSET_MOVE", StringComparison.Ordinal))
                continue;

            string id = string.IsNullOrWhiteSpace(move.StateId) ? move.Id : move.StateId;
            options.Add(new MoveOption(id, ResolveMoveDisplayName(monster, move, owner, id)));
        }

        return options
            .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Id, StringComparer.Ordinal)
            .ToList();
    }

    internal static bool TrySetMoveAtTurn(Creature enemy, int turnIndex, string moveId, out string? error) {
        error = null;
        if (!DevModeState.IsActive) {
            error = "DevMode is not active.";
            return false;
        }

        if (CombatManager.Instance?.IsInProgress != true) {
            error = "Not in combat.";
            return false;
        }

        if (enemy.Monster is not { } monster) {
            error = "Target is not a monster.";
            return false;
        }

        if (!enemy.IsAlive) {
            error = "Target is dead.";
            return false;
        }

        if (turnIndex < 0) {
            error = I18N.T("enemyIntent.edit.invalidTurn", "Invalid turn index.");
            return false;
        }

        if (!TryFindMoveState(monster, moveId, out MoveState? move) || move == null) {
            error = I18N.T("enemyIntent.edit.moveNotFound", "Move not found on this enemy.");
            return false;
        }

        try {
            MonsterIntentOverrides.Set(enemy, turnIndex, moveId);
            if (turnIndex == 0)
                monster.SetMoveImmediate(move, forceTransition: true);
            MonsterIntentOverlayTracker.NotifyChanged();
            return true;
        }
        catch (Exception ex) {
            error = ex.Message;
            return false;
        }
    }

    internal static bool TrySetNextMove(Creature enemy, string moveId, out string? error) =>
        TrySetMoveAtTurn(enemy, 0, moveId, out error);

    internal static string FormatMoveOptionLabel(MoveOption move) {
        if (string.Equals(move.DisplayName, move.Id, StringComparison.OrdinalIgnoreCase))
            return move.DisplayName;
        return $"{move.DisplayName} · {move.Id}";
    }

    internal static int IndexOfCurrentMove(IReadOnlyList<MoveOption> moves, string currentMoveId) {
        for (int i = 0; i < moves.Count; i++) {
            if (string.Equals(moves[i].Id, currentMoveId, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    internal static bool TryFindMoveState(MonsterModel monster, string moveId, out MoveState? move) {
        move = null;
        if (monster.MoveStateMachine?.States == null)
            return false;

        if (monster.MoveStateMachine.States.TryGetValue(moveId, out MonsterState? byKey) && byKey is MoveState keyed)
            move = keyed;

        if (move != null)
            return true;

        foreach (MonsterState state in monster.MoveStateMachine.States.Values) {
            if (state is not MoveState candidate)
                continue;
            if (string.Equals(candidate.StateId, moveId, StringComparison.Ordinal)
                || string.Equals(candidate.Id, moveId, StringComparison.Ordinal)) {
                move = candidate;
                return true;
            }
        }

        return false;
    }

    private static string ResolveMoveDisplayName(
        MonsterModel monster,
        MoveState move,
        Creature owner,
        string moveId) {
        string? localized = MonsterIntentReader.TryResolveMoveLoc(monster, moveId);
        if (localized != null)
            return localized;

        if (moveId.StartsWith("FIRST_", StringComparison.Ordinal)) {
            string suffix = moveId["FIRST_".Length..];
            localized = MonsterIntentReader.TryResolveMoveLoc(monster, suffix);
            if (localized != null)
                return I18N.T("enemyIntent.move.first", "{0} (first)", localized);
        }

        string? intentLabel = TryResolveIntentLabel(move, owner);
        if (intentLabel != null)
            return intentLabel;

        return MonsterIntentReader.FormatMoveIdFallback(moveId);
    }

    private static string? TryResolveIntentLabel(MoveState move, Creature owner) {
        if (move.Intents.Count == 0)
            return null;

        var targets = owner.CombatState?.PlayerCreatures ?? Array.Empty<Creature>();
        foreach (AbstractIntent intent in move.Intents) {
            if (intent.IntentType == IntentType.Hidden)
                continue;

            string? text = DevModeTheme.StripFontSizeBbcode(
                intent.GetIntentLabel(targets, owner).GetFormattedText());
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }
}
