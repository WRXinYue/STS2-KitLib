using System;
using System.Linq;
using KitLib.Hooks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Scripts;

/// <summary>Recursively evaluates a <see cref="ConditionNode"/> tree.</summary>
internal static class ScriptConditionEvaluator {
    public static bool Evaluate(ConditionNode? node, Player? player) {
        if (node == null) return true;

        return node switch {
            AndNode and => and.Children.All(c => Evaluate(c, player)),
            OrNode or => or.Children.Any(c => Evaluate(c, player)),
            NotNode not => !Evaluate(not.Child, player),
            VarCompareCondition vc => EvalVarCompare(vc),
            LeafCondition leaf => EvalLeaf(leaf, player),
            _ => true
        };
    }

    private static bool EvalVarCompare(VarCompareCondition vc) {
        int val = ScriptVariableStore.Get(vc.VarName);
        return vc.Op switch {
            ">" => val > vc.Threshold,
            ">=" => val >= vc.Threshold,
            "<" => val < vc.Threshold,
            "<=" => val <= vc.Threshold,
            "==" or "=" => val == vc.Threshold,
            "!=" => val != vc.Threshold,
            _ => false
        };
    }

    private static bool EvalLeaf(LeafCondition leaf, Player? player) {
        return leaf.Type switch {
            ConditionType.None => true,
            ConditionType.HpBelow => CheckHpPercent(player, leaf.Value, below: true),
            ConditionType.HpAbove => CheckHpPercent(player, leaf.Value, below: false),
            ConditionType.FloorAbove => CheckFloor(leaf.Value, above: true),
            ConditionType.FloorBelow => CheckFloor(leaf.Value, above: false),
            ConditionType.HasPower => CheckHasPower(player, leaf.Value),
            ConditionType.NotHasPower => !CheckHasPower(player, leaf.Value),
            _ => true
        };
    }

    private static bool CheckHpPercent(Player? player, string raw, bool below) {
        if (player?.Creature == null) return false;
        if (!int.TryParse(raw, out int threshold)) return false;
        int maxHp = Math.Max(1, player.Creature.MaxHp);
        int pct = (int)(player.Creature.CurrentHp * 100.0 / maxHp);
        return below ? pct < threshold : pct > threshold;
    }

    private static bool CheckFloor(string raw, bool above) {
        if (!int.TryParse(raw, out int threshold)) return false;
        int floor = GetCurrentFloor();
        return above ? floor > threshold : floor < threshold;
    }

    private static bool CheckHasPower(Player? player, string powerId) {
        if (player?.Creature == null || string.IsNullOrEmpty(powerId)) return false;
        return player.Creature.Powers.Any(p =>
            p != null && string.Equals(p.Id.Entry, powerId, StringComparison.OrdinalIgnoreCase));
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
