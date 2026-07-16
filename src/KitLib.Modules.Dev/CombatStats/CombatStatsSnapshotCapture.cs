using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.EnemyIntent;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace KitLib.CombatStats;

internal static class CombatStatsSnapshotCapture {
    public static List<CreatureState> CaptureLive(CombatState? state, bool includeDefeated = false) {
        if (state == null)
            return new List<CreatureState>();

        var creatures = new List<CreatureState>();
        foreach (Player player in state.Players) {
            if (player?.Creature == null)
                continue;
            if (!includeDefeated && !player.Creature.IsAlive)
                continue;
            creatures.Add(CapturePlayer(player));
        }

        IEnumerable<Creature> enemies = includeDefeated ? state.Enemies : state.HittableEnemies;
        foreach (Creature enemy in enemies) {
            if (!includeDefeated && !enemy.IsAlive)
                continue;
            creatures.Add(CaptureEnemy(enemy));
        }

        return creatures;
    }

    public static TurnSnapshot CaptureTurn(CombatState state, int turn, string phase) => new() {
        Turn = turn,
        Phase = phase,
        Creatures = CaptureLive(state, includeDefeated: phase == "end"),
    };

    public static CreatureState CapturePlayerCreature(Player player) {
        return CapturePlayer(player);
    }

    public static CreatureState CaptureEnemyCreature(Creature enemy) => CaptureEnemy(enemy);

    private static CreatureState CapturePlayer(Player player) {
        var creature = player.Creature!;
        return new CreatureState {
            Key = player.NetId.ToString(),
            DisplayName = ResolvePlayerName(player, creature),
            Side = "player",
            CurrentHp = creature.CurrentHp,
            MaxHp = creature.MaxHp,
            Block = creature.Block,
            Energy = player.PlayerCombatState?.Energy,
            Powers = CapturePowers(creature.Powers),
        };
    }

    private static CreatureState CaptureEnemy(Creature enemy) {
        string key;
        try {
            key = MonsterIntentOverrides.BuildEnemyKey(enemy);
        }
        catch {
            key = enemy.ModelId.Entry;
        }

        return new CreatureState {
            Key = key,
            DisplayName = ResolveEnemyName(enemy),
            Side = "enemy",
            CurrentHp = enemy.CurrentHp,
            MaxHp = enemy.MaxHp,
            Block = enemy.Block,
            Powers = CapturePowers(enemy.Powers),
            IntentSummary = ResolveIntentSummary(enemy),
        };
    }

    private static string ResolvePlayerName(Player player, Creature creature) {
        try {
            string? title = player.Character?.Title.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }
        catch { }

        try {
            if (!string.IsNullOrWhiteSpace(creature.Name))
                return creature.Name;
        }
        catch { }

        return player.NetId.ToString();
    }

    private static string ResolveEnemyName(Creature enemy) {
        try {
            string? title = enemy.Monster?.Title?.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }
        catch { }

        try {
            if (!string.IsNullOrWhiteSpace(enemy.Name))
                return enemy.Name;
        }
        catch { }

        try {
            return enemy.ModelId.Entry;
        }
        catch {
            return "Enemy";
        }
    }

    private static string? ResolveIntentSummary(Creature enemy) {
        if (enemy.Monster?.NextMove is not MoveState move || move.Intents.Count == 0)
            return null;

        var targets = enemy.CombatState?.PlayerCreatures ?? Array.Empty<Creature>();
        var parts = new List<string>();
        foreach (var intent in move.Intents) {
            if (intent.IntentType == IntentType.Hidden)
                continue;

            string? text = null;
            try {
                text = BbcodeTextHelper.StripFontSizeBbcode(
                    intent.GetIntentLabel(targets, enemy).GetFormattedText());
            }
            catch {
                // fall through
            }

            parts.Add(string.IsNullOrWhiteSpace(text) ? intent.IntentType.ToString() : text);
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static List<PowerState> CapturePowers(IEnumerable<PowerModel?> powers) {
        var list = new List<PowerState>();
        foreach (var power in powers) {
            if (power == null || !power.IsVisible || power.Amount <= 0)
                continue;

            list.Add(CombatStatsDisplayNames.CapturePowerState(power));
        }

        return list;
    }
}
