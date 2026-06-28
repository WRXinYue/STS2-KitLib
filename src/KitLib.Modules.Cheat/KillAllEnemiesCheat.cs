using KitLib.Actions;
using MegaCrit.Sts2.Core.Helpers;

namespace KitLib.Cheat;

/// <summary>
/// Reuses <see cref="CombatEnemyActions.KillAllEnemies"/> (same path as the combat sidebar button).
/// When enabled: kill once immediately if already in combat, and once at each combat setup.
/// </summary>
public static class KillAllEnemiesCheat {
    public static bool IsEnabled => KitLibState.EnemyCheats.AutoKillAllEnemies;

    public static void SetEnabled(bool enabled) {
        KitLibState.EnemyCheats.AutoKillAllEnemies = enabled;
        if (enabled)
            TryApply();
    }

    public static void TryApply() {
        if (!KitLibState.CheatsInRun || !IsEnabled)
            return;
        TaskHelper.RunSafely(CombatEnemyActions.KillAllEnemies());
    }
}
