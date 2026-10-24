using System.Threading.Tasks;
using KitLib.Actions;
using KitLib.Game;
using MegaCrit.Sts2.Core.Commands;
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
        TaskHelper.RunSafely(KillAllThenSettle());
    }

    // A force kill skips the normal death pipeline, so the room would sit in combat until the turn ends.
    // Ending the turn lets the game resolve the victory itself; retry on the main thread while deaths settle.
    private static async Task KillAllThenSettle() {
        await CombatEnemyActions.KillAllEnemies();

        for (int i = 0; i < 40; i++) {
            if (await GameMainThread.InvokeAsync(TryEndTurn))
                return;

            await Task.Delay(50);
        }
    }

    private static bool TryEndTurn() {
        if (!Sts2CombatCompat.IsCombatPlayPhaseActive())
            return false;
        if (!RunContext.TryGetRunAndPlayer(out _, out var player) || player == null)
            return true;

        PlayerCmd.EndTurn(player, canBackOut: false);
        return true;
    }
}
