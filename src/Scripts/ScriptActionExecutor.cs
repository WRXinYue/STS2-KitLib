using System;
using System.Linq;
using KitLib.Actions;
using KitLib.Hooks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Scripts;

/// <summary>Recursively executes an <see cref="ActionNode"/> tree.</summary>
internal static class ScriptActionExecutor {
    private const int MaxDepth = 64;

    public static void Execute(ActionNode? node, Player player, int depth = 0) {
        if (node == null || depth > MaxDepth) return;

        switch (node) {
            case SequenceNode seq:
                foreach (var step in seq.Steps)
                    Execute(step, player, depth + 1);
                break;

            case IfNode ifn:
                if (ScriptConditionEvaluator.Evaluate(ifn.Condition, player))
                    Execute(ifn.Then, player, depth + 1);
                else if (ifn.Else != null)
                    Execute(ifn.Else, player, depth + 1);
                break;

            case ForEachEnemyNode fe:
                Execute(fe.Body, player, depth + 1);
                break;

            case RepeatNode rp:
                int count = Math.Clamp(rp.Count, 0, 100);
                for (int i = 0; i < count; i++)
                    Execute(rp.Body, player, depth + 1);
                break;

            case SetVarNode sv:
                ScriptVariableStore.Set(sv.VarName, sv.Value);
                break;

            case IncrVarNode iv:
                ScriptVariableStore.Increment(iv.VarName, iv.Delta);
                break;

            case BasicActionNode ba:
                ExecuteBasic(ba, player);
                break;
        }
    }

    private static void ExecuteBasic(BasicActionNode action, Player player) {
        try {
            switch (action.Type) {
                case ActionType.ApplyPower:
                    ExecuteApplyPower(action, player);
                    break;
                case ActionType.AddCard:
                    ExecuteAddCard(action, player);
                    break;
                case ActionType.SaveSlot:
                    SaveSlotManager.SaveToSlot(0);
                    break;
                case ActionType.UsePotion:
                    ExecuteUsePotion(action, player);
                    break;
            }
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[Script] Action {action.Type} failed: {ex.Message}");
        }
    }

    private static void ExecuteApplyPower(BasicActionNode action, Player player) {
        if (!CombatManager.Instance.IsInProgress) return;

        var power = FindPower(action.TargetId);
        if (power == null) return;

        var target = action.Target switch {
            HookTargetType.Player => PowerTarget.Self,
            HookTargetType.AllEnemies => PowerTarget.AllEnemies,
            HookTargetType.Allies => PowerTarget.Allies,
            _ => PowerTarget.Self
        };

        TaskHelper.RunSafely(PowerActions.AddPower(player, power, action.Amount, target));
    }

    private static void ExecuteAddCard(BasicActionNode action, Player player) {
        var card = FindCard(action.TargetId);
        if (card == null) return;
        if (!RunContext.TryGetRunAndPlayer(out var state, out _)) return;
        TaskHelper.RunSafely(CardActions.Add(state, player, card).RunAsync());
    }

    private static void ExecuteUsePotion(BasicActionNode action, Player player) {
        if (string.IsNullOrEmpty(action.TargetId)) return;
        var potion = player.Potions?.FirstOrDefault(p =>
            p != null && string.Equals(p.Id.Entry, action.TargetId, StringComparison.OrdinalIgnoreCase));
        if (potion == null) return;
        potion.EnqueueManualUse(player.Creature);
    }

    private static PowerModel? FindPower(string id) {
        if (string.IsNullOrEmpty(id)) return null;
        return PowerActions.GetAllPowers()
            .FirstOrDefault(p => string.Equals(((AbstractModel)p).Id.Entry, id, StringComparison.OrdinalIgnoreCase));
    }

    private static CardModel? FindCard(string id) {
        if (string.IsNullOrEmpty(id)) return null;
        return ModelDb.AllCards
            .FirstOrDefault(c => string.Equals(((AbstractModel)c).Id.Entry, id, StringComparison.OrdinalIgnoreCase));
    }
}
