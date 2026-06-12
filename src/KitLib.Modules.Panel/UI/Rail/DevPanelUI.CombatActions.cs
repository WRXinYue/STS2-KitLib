using System;
using MegaCrit.Sts2.Core.Helpers;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal static void RunCombatAction(Func<System.Threading.Tasks.Task> action, Action? onCompleted = null) {
        TaskHelper.RunSafely(WrapCombatAction(action, onCompleted));
    }

    private static async System.Threading.Tasks.Task WrapCombatAction(
        Func<System.Threading.Tasks.Task> action,
        Action? onCompleted) {
        KitLog.Info("CombatAdd", "RunCombatAction starting");
        try {
            await action();
            KitLog.Info("CombatAdd", "RunCombatAction finished");
        }
        catch (Exception ex) {
            KitLog.Warn("CombatAdd", $"RunCombatAction failed: {ex}");
            throw;
        }
        finally {
            EnemySelectUI.RefreshMapCombatDetailIfOpen();
            onCompleted?.Invoke();
        }
    }
}
