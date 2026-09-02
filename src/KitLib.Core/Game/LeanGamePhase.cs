using KitLib.AI.Core.Schema;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Game;

internal static class LeanGamePhase {
    public static GamePhase Current() {
        if (GameOverlayPhase.HasActiveCardRewardScreen())
            return GamePhase.CardReward;
        if (GameOverlayPhase.HasActiveRelicSelectionScreen())
            return GamePhase.RelicSelection;

        var cm = CombatManager.Instance;
        var overlay = NOverlayStack.Instance?.Peek();
        if (overlay != null) {
            if (overlay is NRewardsScreen rewardsScreen) {
                Player? rewardPlayer = null;
                if (RunContext.TryGetRunAndPlayer(out var runState, out var p))
                    rewardPlayer = p;

                var hasCollectable = GameOverlayPhase.HasClickableRewards(
                    rewardsScreen, rewardPlayer?.HasOpenPotionSlots ?? false);

                if (GameOverlayPhase.RewardsReadyForMap(rewardsScreen, rewardPlayer))
                    return GamePhase.MapSelection;

                if (hasCollectable)
                    return GamePhase.RewardScreen;

                if (TryGetInRoomPhase(runState?.CurrentRoom?.RoomType) is { } inRoomPhase)
                    return inRoomPhase;

                return GamePhase.RewardScreen;
            }

            return overlay switch {
                NChooseARelicSelection => GamePhase.RelicSelection,
                NCardRewardSelectionScreen => GamePhase.CardReward,
                NDeckCardSelectScreen => GamePhase.CardReward,
                NChooseACardSelectionScreen => GamePhase.CardReward,
                NGameOverScreen => GamePhase.GameOver,
                _ => cm is { IsInProgress: true }
                    ? GamePhase.Combat
                    : GamePhase.Unknown,
            };
        }

        if (!RunContext.TryGetRunAndPlayer(out var state, out _))
            return GamePhase.None;

        if (cm is { IsInProgress: true })
            return GamePhase.Combat;

        if (cm != null && !cm.IsInProgress
            && state.CurrentRoom?.RoomType is RoomType.Monster or RoomType.Elite or RoomType.Boss) {
            var stack = NOverlayStack.Instance;
            if (stack == null || stack.ScreenCount == 0) {
                if (NMapScreen.Instance is not { IsOpen: true })
                    return GamePhase.PostCombatTransition;
            }
        }

        if (NMapScreen.Instance is { IsOpen: true })
            return GamePhase.MapSelection;

        var room = state.CurrentRoom;
        if (room != null) {
            return room.RoomType switch {
                RoomType.Event => GamePhase.EventChoice,
                RoomType.Shop => GamePhase.Shop,
                RoomType.RestSite => GamePhase.RestSite,
                RoomType.Treasure => GamePhase.TreasureRoom,
                _ => GamePhase.Unknown,
            };
        }

        return GamePhase.Unknown;
    }

    static GamePhase? TryGetInRoomPhase(RoomType? roomType) =>
        roomType switch {
            RoomType.RestSite => GamePhase.RestSite,
            RoomType.Shop => GamePhase.Shop,
            RoomType.Event => GamePhase.EventChoice,
            RoomType.Treasure => GamePhase.TreasureRoom,
            _ => null,
        };
}
