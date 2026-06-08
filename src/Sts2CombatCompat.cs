using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

#if STS2_BETA
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
#endif

namespace KitLib;

/// <summary>
/// Stable vs Steam-beta combat API shims (IsPlayPhase removal, history entry fields, ICombatState).
/// </summary>
internal static class Sts2CombatCompat {
    private const BindingFlags EntryPropFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static int GetHistoryRoundNumber(CombatHistoryEntry entry) {
#if STS2_BETA
        return ReadEntryProperty<int>(entry, "RoundNumber");
#else
        return entry.RoundNumber;
#endif
    }

    public static CombatSide GetHistoryCurrentSide(CombatHistoryEntry entry) {
#if STS2_BETA
        return ReadEntryProperty<CombatSide>(entry, "CurrentSide");
#else
        return entry.CurrentSide;
#endif
    }

    public static bool IsCombatPlayPhase(CombatManager? cm) {
        if (cm is not { IsInProgress: true })
            return false;
#if STS2_BETA
        return RunManager.Instance?.ActionQueueSynchronizer?.CombatState
            == ActionSynchronizerCombatState.PlayPhase;
#else
        return cm.IsPlayPhase;
#endif
    }

    public static bool IsCombatPlayPhaseActive() =>
        IsCombatPlayPhase(CombatManager.Instance);

    public static int GetCombatRoundNumber() =>
        CombatManager.Instance?.DebugOnlyGetState()?.RoundNumber ?? 0;

    public static bool IsPlayerReadyToBeginEnemyTurn(CombatManager cm, Player player) {
        var method = AccessTools.Method(typeof(CombatManager), "IsPlayerReadyToBeginEnemyTurn", [typeof(Player)]);
        return method != null && (bool)method.Invoke(cm, [player])!;
    }

    public static CombatState? GetCreatureCombatState(Creature creature) {
#if STS2_BETA
        return creature.CombatState as CombatState;
#else
        return creature.CombatState;
#endif
    }

    public static void ForcePlayPhase() {
#if STS2_BETA
        RunManager.Instance?.ActionQueueSynchronizer?.SetCombatState(
            ActionSynchronizerCombatState.PlayPhase);
#else
        TrySetCombatManagerBool(CombatManager.Instance, "IsPlayPhase", true);
#endif
    }

#if STS2_BETA
    static T ReadEntryProperty<T>(CombatHistoryEntry entry, string name) {
        object? raw = typeof(CombatHistoryEntry)
            .GetProperty(name, EntryPropFlags)
            ?.GetValue(entry);
        return raw is T value ? value : default!;
    }
#else
    static void TrySetCombatManagerBool(CombatManager? cm, string name, bool value) {
        if (cm == null) return;
        var prop = typeof(CombatManager).GetProperty(name, EntryPropFlags);
        if (prop?.CanWrite == true)
            prop.SetValue(cm, value);
    }
#endif
}
