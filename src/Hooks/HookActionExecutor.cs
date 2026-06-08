using System;
using System.Linq;
using System.Threading.Tasks;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Hooks;

internal static class HookActionExecutor {
    public static void Execute(HookAction action, Player player) {
        try {
            switch (action.Type) {
                case ActionType.ApplyPower:
                    ExecuteApplyPower(action, player);
                    break;
                case ActionType.AddCard:
                    ExecuteAddCard(action, player);
                    break;
                case ActionType.SaveSlot:
                    ExecuteSaveSlot(action);
                    break;
                case ActionType.UsePotion:
                    ExecuteUsePotion(action, player);
                    break;
            }
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[Hook] Action {action.Type} failed: {ex.Message}");
        }
    }

    private static void ExecuteApplyPower(HookAction action, Player player) {
        if (!CombatManager.Instance.IsInProgress) return;

        var power = FindPower(action.TargetId);
        if (power == null) {
            MainFile.Logger.Warn($"[Hook] Power not found: {action.TargetId}");
            return;
        }

        var target = MapTarget(action.Target);
        TaskHelper.RunSafely(PowerActions.AddPower(player, power, action.Amount, target));
    }

    private static void ExecuteAddCard(HookAction action, Player player) {
        var card = FindCard(action.TargetId);
        if (card == null) {
            MainFile.Logger.Warn($"[Hook] Card not found: {action.TargetId}");
            return;
        }

        if (!RunContext.TryGetRunAndPlayer(out var state, out _)) return;

        TaskHelper.RunSafely(CardActions.Add(state, player, card).RunAsync());
    }

    private static void ExecuteSaveSlot(HookAction action) {
        SaveSlotManager.SaveToSlot(action.SlotIndex);
    }

    private static void ExecuteUsePotion(HookAction action, Player player) {
        if (string.IsNullOrEmpty(action.TargetId)) return;

        var potion = player.Potions?.FirstOrDefault(p =>
            p != null && string.Equals(p.Id.Entry, action.TargetId, StringComparison.OrdinalIgnoreCase));

        if (potion == null) {
            MainFile.Logger.Warn($"[Hook] Potion not found in inventory: {action.TargetId}");
            return;
        }

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

    private static PowerTarget MapTarget(HookTargetType target) => target switch {
        HookTargetType.Player => PowerTarget.Self,
        HookTargetType.AllEnemies => PowerTarget.AllEnemies,
        HookTargetType.Allies => PowerTarget.Allies,
        _ => PowerTarget.Self
    };
}
