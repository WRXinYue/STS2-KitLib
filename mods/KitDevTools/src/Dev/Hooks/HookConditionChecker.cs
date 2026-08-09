using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Hooks;

internal static class HookConditionChecker {
    /// <summary>Returns true when ALL conditions are met (AND logic).</summary>
    public static bool CheckAll(List<HookCondition> conditions, Player? player) {
        if (conditions.Count == 0) return true;
        foreach (var c in conditions) {
            if (!Check(c, player)) return false;
        }
        return true;
    }

    private static bool Check(HookCondition condition, Player? player) {
        return condition.Type switch {
            ConditionType.None => true,
            ConditionType.HpBelow => CheckHpBelow(condition, player),
            ConditionType.HpAbove => CheckHpAbove(condition, player),
            ConditionType.FloorAbove => CheckFloorAbove(condition),
            ConditionType.FloorBelow => CheckFloorBelow(condition),
            ConditionType.HasPower => CheckHasPower(condition, player),
            ConditionType.NotHasPower => !CheckHasPower(condition, player),
            _ => true
        };
    }

    private static bool CheckHpBelow(HookCondition c, Player? player) {
        if (player?.Creature == null) return false;
        if (!int.TryParse(c.Value, out int threshold)) return false;
        int maxHp = Math.Max(1, player.Creature.MaxHp);
        int pct = (int)(player.Creature.CurrentHp * 100.0 / maxHp);
        return pct < threshold;
    }

    private static bool CheckHpAbove(HookCondition c, Player? player) {
        if (player?.Creature == null) return false;
        if (!int.TryParse(c.Value, out int threshold)) return false;
        int maxHp = Math.Max(1, player.Creature.MaxHp);
        int pct = (int)(player.Creature.CurrentHp * 100.0 / maxHp);
        return pct > threshold;
    }

    private static bool CheckFloorAbove(HookCondition c) {
        if (!int.TryParse(c.Value, out int threshold)) return false;
        var floor = GetCurrentFloor();
        return floor > threshold;
    }

    private static bool CheckFloorBelow(HookCondition c) {
        if (!int.TryParse(c.Value, out int threshold)) return false;
        var floor = GetCurrentFloor();
        return floor < threshold;
    }

    private static bool CheckHasPower(HookCondition c, Player? player) {
        if (player?.Creature == null || string.IsNullOrEmpty(c.Value)) return false;
        return player.Creature.Powers.Any(p =>
            p != null && string.Equals(p.Id.Entry, c.Value, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetCurrentFloor() {
        try {
            var rm = RunManager.Instance;
            if (rm == null || !rm.IsInProgress) return 0;
            var state = rm.DebugOnlyGetState();
            return state?.ActFloor ?? 0;
        }
        catch { return 0; }
    }
}
