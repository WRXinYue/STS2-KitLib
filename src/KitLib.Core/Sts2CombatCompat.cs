using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib;

/// <summary>
/// Combat API shims for STS2 v0.107.1+.
/// </summary>
internal static class Sts2CombatCompat {
    private const BindingFlags EntryPropFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static int GetHistoryRoundNumber(CombatHistoryEntry entry) =>
        ReadEntryProperty<int>(entry, "RoundNumber");

    public static CombatSide GetHistoryCurrentSide(CombatHistoryEntry entry) =>
        ReadEntryProperty<CombatSide>(entry, "CurrentSide");

    public static bool IsCombatPlayPhase(CombatManager? cm) {
        if (cm is not { IsInProgress: true })
            return false;

        return IsActionSynchronizerPlayPhase();
    }

    public static bool IsCombatPlayPhaseActive() =>
        IsCombatPlayPhase(CombatManager.Instance);

    public static int GetCombatRoundNumber() =>
        CombatManager.Instance?.DebugOnlyGetState()?.RoundNumber ?? 0;

    public static bool IsPlayerReadyToBeginEnemyTurn(CombatManager cm, Player player) {
        var field = AccessTools.Field(typeof(CombatManager), "_playersReadyToBeginEnemyTurn");
        if (field?.GetValue(cm) is not System.Collections.IEnumerable readyPlayers)
            return false;

        foreach (var entry in readyPlayers) {
            if (entry is Player ready && ReferenceEquals(ready, player))
                return true;
        }

        return false;
    }

    public static CombatState? GetCreatureCombatState(Creature creature) =>
        creature.CombatState as CombatState;

    public static void ForcePlayPhase() => SetActionSynchronizerPlayPhase();

    static bool IsActionSynchronizerPlayPhase() {
        var sync = GetActionQueueSynchronizer();
        if (sync == null)
            return false;
        var prop = sync.GetType().GetProperty("CombatState", EntryPropFlags);
        var value = prop?.GetValue(sync);
        return value != null && string.Equals(value.ToString(), "PlayPhase", StringComparison.Ordinal);
    }

    static void SetActionSynchronizerPlayPhase() {
        var sync = GetActionQueueSynchronizer();
        if (sync == null)
            return;
        var method = sync.GetType().GetMethod("SetCombatState", EntryPropFlags);
        if (method == null)
            return;
        var enumType = method.GetParameters()[0].ParameterType;
        var playPhase = Enum.Parse(enumType, "PlayPhase");
        method.Invoke(sync, [playPhase]);
    }

    static object? GetActionQueueSynchronizer() {
        var run = RunManager.Instance;
        if (run == null)
            return null;
        var prop = run.GetType().GetProperty("ActionQueueSynchronizer", EntryPropFlags);
        return prop?.GetValue(run);
    }

    static T ReadEntryProperty<T>(CombatHistoryEntry entry, string name) {
        object? raw = typeof(CombatHistoryEntry)
            .GetProperty(name, EntryPropFlags)
            ?.GetValue(entry);
        return raw is T value ? value : default!;
    }
}
