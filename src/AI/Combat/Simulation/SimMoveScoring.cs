using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;

namespace KitLib.AI.Combat.Simulation;

/// <summary>Bridges sim/beam actions to <see cref="IAiMoveModifier"/> via <see cref="GameAction"/>.</summary>
public static class SimMoveScoring {
    public static int WithModifiers(
        CombatState state,
        SimCombatAction action,
        int baseScore,
        JsonObject? rootSnapshot) {
        if (rootSnapshot == null || baseScore <= int.MinValue + 1)
            return baseScore;

        var move = ToGameAction(action, state);
        var snap = BuildModifierSnapshot(state, rootSnapshot);
        return AiMoveModifierHub.ApplyModifiers(snap, move, baseScore);
    }

    public static int OpeningModifierBonus(
        CombatState rootState,
        SimCombatAction firstAction,
        JsonObject? rootSnapshot) =>
        WithModifiers(rootState, firstAction, 0, rootSnapshot);

    public static GameAction ToGameAction(SimCombatAction action, CombatState state) {
        if (action.Kind == SimActionKind.EndTurn)
            return new GameAction { Type = ActionType.EndTurn };

        if (action.Kind == SimActionKind.UsePotion)
            return new GameAction {
                Type = ActionType.UsePotion,
                TargetIndex = action.PotionSlot,
                SecondaryIndex = action.EnemyIndex,
            };

        if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count)
            return new GameAction { Type = ActionType.PlayCard, TargetIndex = action.HandIndex };

        var card = state.Hand[action.HandIndex];
        return new GameAction {
            Type = ActionType.PlayCard,
            TargetIndex = action.HandIndex,
            SecondaryIndex = action.EnemyIndex,
            Reason = card.Name,
        };
    }

    static JsonObject BuildModifierSnapshot(CombatState state, JsonObject rootSnapshot) {
        var snap = new JsonObject {
            ["currentHp"] = state.PlayerHp,
            ["maxHp"] = state.PlayerMaxHp,
            ["combat"] = new JsonObject {
                ["playerBlock"] = state.PlayerBlock,
                ["currentEnergy"] = state.Energy,
                ["maxEnergy"] = state.MaxEnergy,
                ["turnNumber"] = state.TurnNumber,
                ["hand"] = state.ToHandJson(),
                ["enemies"] = new JsonArray(state.Enemies.Select(EnemyToJson).ToArray()),
            },
        };

        if (rootSnapshot["characterId"] != null)
            snap["characterId"] = rootSnapshot["characterId"]?.DeepClone();

        if (rootSnapshot["extensions"] is JsonObject extensions)
            snap["extensions"] = extensions.DeepClone();

        if (rootSnapshot["totalFloor"] != null)
            snap["totalFloor"] = rootSnapshot["totalFloor"]?.DeepClone();

        return snap;
    }

    static JsonObject EnemyToJson(CombatEnemy enemy) => new() {
        ["index"] = enemy.Index,
        ["currentHp"] = enemy.CurrentHp,
        ["maxHp"] = enemy.MaxHp,
        ["block"] = enemy.Block,
        ["isAlive"] = enemy.IsAlive,
        ["intentDamage"] = enemy.IntentDamage,
        ["monsterId"] = enemy.MonsterId,
        ["powers"] = enemy.Vulnerable > 0
            ? new JsonArray(new JsonObject { ["id"] = "VULNERABLE", ["amount"] = enemy.Vulnerable })
            : new JsonArray(),
    };
}
