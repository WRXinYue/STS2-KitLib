using System.Collections.Generic;
using System.Linq;
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

    public static int GetHistoryPlayerTurnNumber(CombatHistoryEntry entry, ulong playerNetId) {
        if (TryReadPlayerTurnNumbers(entry, out var dict) && dict.TryGetValue(playerNetId, out int turn))
            return turn;
        return 0;
    }

    public static int GetHistoryActorPlayerTurnNumber(CombatHistoryEntry entry) {
        if (entry.Actor?.Player != null)
            return GetHistoryPlayerTurnNumber(entry, entry.Actor.Player.NetId);
        return 0;
    }

    public static int GetHistoryMaxPlayerTurnNumber(CombatHistoryEntry entry) {
        if (!TryReadPlayerTurnNumbers(entry, out var dict) || dict.Count == 0)
            return 0;
        return dict.Values.Max();
    }

    /// <summary>Player-facing turn label (matches in-game "Turn N" banner).</summary>
    public static int GetPrimaryPlayerTurnNumber(CombatState state) {
        int max = 0;
        foreach (Player player in state.Players) {
            int turn = player.PlayerCombatState?.TurnNumber ?? 0;
            if (turn > max)
                max = turn;
        }
        return max > 0 ? max : Math.Max(1, state.RoundNumber);
    }

    public static int ResolveHistoryDisplayTurn(CombatHistoryEntry entry) {
        CombatSide side = GetHistoryCurrentSide(entry);
        if (side == CombatSide.Player) {
            int actorTurn = GetHistoryActorPlayerTurnNumber(entry);
            if (actorTurn > 0)
                return actorTurn;
            int maxTurn = GetHistoryMaxPlayerTurnNumber(entry);
            if (maxTurn > 0)
                return maxTurn;
        }

        int round = GetHistoryRoundNumber(entry);
        return round > 0 ? round : 1;
    }

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

    static bool TryReadPlayerTurnNumbers(CombatHistoryEntry entry, out Dictionary<ulong, int> dict) {
        dict = null!;
        var field = typeof(CombatHistoryEntry).GetField("_playerTurnNumbers", EntryPropFlags);
        if (field?.GetValue(entry) is not Dictionary<ulong, int> numbers)
            return false;
        dict = numbers;
        return true;
    }
}
