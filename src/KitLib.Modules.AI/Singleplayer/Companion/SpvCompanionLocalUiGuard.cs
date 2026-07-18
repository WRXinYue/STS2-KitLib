using KitLib.AI.Core.Schema;
using KitLib.AI.Sts2.Helpers;
using KitLib.AI.Sts2.Snapshots;
using KitLib.Multiplayer.PseudoCoop;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Singleplayer.Companion;

/// <summary>SP companions share one local UI; block UI automation while the human owns it.</summary>
internal static class SpvCompanionLocalUiGuard {
    internal static bool BlocksCompanionGameLoop(
        GamePhase phase,
        RunState state,
        Player local,
        Player companion) {
        switch (phase) {
            case GamePhase.MapSelection:
            case GamePhase.CardReward:
            case GamePhase.RelicSelection:
                return true;

            case GamePhase.RewardScreen:
            case GamePhase.PostCombatTransition:
                return LocalHasPendingRewards(state, local);

            case GamePhase.EventChoice:
            case GamePhase.Shop:
            case GamePhase.RestSite:
            case GamePhase.TreasureRoom:
                return !PseudoCoopActionQueue.HasQueuedActions(companion.NetId)
                    && !PseudoCoopActionQueue.HasInFlightAction(companion.NetId);
        }

        return false;
    }

    static bool LocalHasPendingRewards(RunState state, Player local) {
        if (NOverlayStack.Instance?.Peek() is not NRewardsScreen screen)
            return false;

        var snapshot = GameSnapshot.Capture(state, local, GamePhase.RewardScreen);
        return OverlayPhaseHelper.HasClickableRewards(screen, local.HasOpenPotionSlots, snapshot);
    }
}
