using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib;

/// <summary>
/// Combat helpers. Play-phase checks use public API; history and ready-set use reflection on private game members.
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

        return RunManager.Instance?.ActionQueueSynchronizer?.CombatState
            == ActionSynchronizerCombatState.PlayPhase;
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

    public static void ForcePlayPhase() =>
        RunManager.Instance?.ActionQueueSynchronizer?
            .SetCombatState(ActionSynchronizerCombatState.PlayPhase);

    static T ReadEntryProperty<T>(CombatHistoryEntry entry, string name) {
        object? raw = typeof(CombatHistoryEntry)
            .GetProperty(name, EntryPropFlags)
            ?.GetValue(entry);
        return raw is T value ? value : default!;
    }
}
