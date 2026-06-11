using System;
using System.Collections.Generic;
using KitLib.Host;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Hooks;

/// <summary>
/// Central dispatcher: receives trigger events, checks conditions, and executes actions.
/// </summary>
internal static class HookManager {
    /// <summary>
    /// Fire a trigger. All enabled hooks matching the trigger are evaluated;
    /// those whose conditions pass will have their actions executed.
    /// </summary>
    public static void Fire(TriggerType trigger, Player? player) {
        if (!KitLibState.IsActive) return;
        if (KitLibCheatOps.IsMpHooksDisabledInMultiplayer?.Invoke() == true) return;

        List<HookEntry> hooks;
        try { hooks = SettingsStore.Current.Hooks; }
        catch { return; }

        if (hooks == null || hooks.Count == 0) return;
        if (player == null && !RunContext.TryGetRunAndPlayer(out _, out player)) return;

        foreach (var hook in hooks) {
            if (!hook.Enabled || hook.Trigger != trigger) continue;

            try {
                if (!HookConditionChecker.CheckAll(hook.Conditions, player)) continue;

                foreach (var action in hook.Actions)
                    HookActionExecutor.Execute(action, player!);
            }
            catch (Exception ex) {
                KitLog.Warn("Hook", $"Error executing '{hook.Name}' ({trigger}): {ex.Message}");
            }
        }
    }
}
