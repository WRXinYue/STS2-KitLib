using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace KitLib.Singleplayer.Companion;

/// <summary>
/// Mid-combat joins only get a draw pile via <see cref="Player.PopulateCombatState"/>.
/// Without <see cref="CombatManager.SetupPlayerTurn"/> they have an empty hand for the current turn.
/// </summary>
internal static class SpvCompanionCombatTurnBootstrap {
    static readonly MethodInfo SetupPlayerTurnMethod =
        AccessTools.Method(typeof(CombatManager), "SetupPlayerTurn")!;

    internal static async Task TryBootstrapMidCombatTurnAsync(Player companion) {
        var cm = CombatManager.Instance;
        var combat = cm?.DebugOnlyGetState();
        if (cm == null || combat == null || combat.CurrentSide != CombatSide.Player)
            return;
        if (!Sts2CombatCompat.IsCombatPlayPhase(cm))
            return;
        if (!LocalContext.NetId.HasValue)
            return;

        var handCount = companion.PlayerCombatState?.Hand?.Cards.Count ?? 0;
        if (handCount > 0)
            return;

        try {
            SyncTurnNumber(companion, combat);

            var ctx = new HookPlayerChoiceContext(
                companion,
                LocalContext.NetId.Value,
                GameActionType.CombatPlayPhaseOnly);
            var setupTask = (Task)SetupPlayerTurnMethod.Invoke(cm, [companion, ctx])!;
            await setupTask;
        }
        catch (System.Exception ex) {
            KitLog.Warn("SpCompanion", $"Mid-combat turn bootstrap failed netId={companion.NetId}: {ex.Message}");
        }
    }

    static void SyncTurnNumber(Player companion, CombatState combat) {
        var pcs = companion.PlayerCombatState;
        if (pcs == null)
            return;

        int targetTurn = combat.Players
            .Where(p => p.NetId != companion.NetId && p.Creature.IsAlive)
            .Select(p => p.PlayerCombatState?.TurnNumber ?? 1)
            .DefaultIfEmpty(1)
            .Max();

        while (pcs.TurnNumber < targetTurn)
            pcs.IncrementTurnNumber();
    }
}
