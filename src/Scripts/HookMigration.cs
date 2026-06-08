using System.Collections.Generic;
using System.Linq;
using KitLib.Hooks;
using KitLib.Settings;

namespace KitLib.Scripts;

/// <summary>
/// One-click migration: converts existing <see cref="HookEntry"/> rules
/// into equivalent <see cref="ScriptEntry"/> JSON files.
/// </summary>
internal static class HookMigration {
    public static int MigrateAll() {
        var hooks = SettingsStore.Current.Hooks;
        if (hooks == null || hooks.Count == 0) return 0;

        int count = 0;
        foreach (var hook in hooks) {
            var script = Convert(hook);
            var fileName = SanitizeName(hook.Name, count) + ".json";
            ScriptManager.SaveScript(script, fileName);
            count++;
        }

        ScriptManager.Reload();
        return count;
    }

    public static ScriptEntry Convert(HookEntry hook) {
        ConditionNode? rootCondition = null;
        if (hook.Conditions.Count > 0) {
            var leaves = hook.Conditions
                .Select(c => (ConditionNode)new LeafCondition(c.Type, c.Value))
                .ToList();
            rootCondition = leaves.Count == 1 ? leaves[0] : new AndNode(leaves);
        }

        ActionNode? rootAction = null;
        if (hook.Actions.Count > 0) {
            var actions = hook.Actions
                .Select(a => (ActionNode)new BasicActionNode(a.Type, a.TargetId, a.Amount, a.Target))
                .ToList();
            rootAction = actions.Count == 1 ? actions[0] : new SequenceNode(actions);
        }

        return new ScriptEntry {
            Name = hook.Name,
            Trigger = hook.Trigger,
            RootCondition = rootCondition,
            RootAction = rootAction,
            Enabled = hook.Enabled,
        };
    }

    private static string SanitizeName(string name, int index) {
        if (string.IsNullOrWhiteSpace(name))
            return $"migrated_{index}";
        var safe = new string(name
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')
            .ToArray());
        return string.IsNullOrEmpty(safe) ? $"migrated_{index}" : safe;
    }
}
