using KitLib;
using KitLib.Multiplayer.PseudoCoop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Singleplayer.Companion;

/// <summary>Per-player combat turn signals for SP companions (never <see cref="PlayerCmd.EndTurn"/>).</summary>
internal static class SpvCompanionCombatActions {
    internal static void SignalEndTurn(Player companion) {
        if (!SpvCompanionRegistry.IsCompanion(companion) || !SpvCompanionRegistry.IsSingleplayerRun())
            return;

        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm))
            return;
        if (companion.Creature.IsDead)
            return;
        if (cm.IsPlayerReadyToEndTurn(companion))
            return;
        if (cm.IsExecutingCardOrPotionEffect(companion))
            return;
        if (PseudoCoopActionQueue.HasQueuedEndTurn(companion.NetId))
            return;
        if (PseudoCoopActionQueue.HasPendingCombatActions(companion.NetId))
            return;

        // SP has one process: enqueue uses action.OwnerId queue, but local end-turn calls
        // StartCancellingAllPlayerDrivenCombatActions and drops EndPlayerTurnAction before it runs.
        cm.SetReadyToEndTurn(companion, canBackOut: false);
    }
}
