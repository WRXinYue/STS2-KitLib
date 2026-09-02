using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Game;

internal static class GameOverlayPhase {
    public static bool HasActiveCardRewardScreen() =>
        FindCardRewardScreen() != null;

    public static bool HasActiveRelicSelectionScreen() =>
        FindRelicSelectionScreen() != null;

    public static bool RewardsReadyForMap(NRewardsScreen screen, Player? player) {
        if (NMapScreen.Instance is not { IsOpen: true })
            return false;

        if (HasActiveCardRewardScreen())
            return false;

        if (screen.IsComplete)
            return true;

        if (RunManager.Instance?.DebugOnlyGetState()?.CurrentRoom is CombatRoom)
            return true;

        return !HasClickableRewards(screen, player?.HasOpenPotionSlots ?? false);
    }

    public static bool HasClickableRewards(NRewardsScreen screen, bool hasPotionSlots) =>
        GameUi.FindAll<NRewardButton>((Node)screen)
            .Any(b => {
                if (!b.IsEnabled) return false;
                if (b.Reward is not PotionReward)
                    return true;
                return hasPotionSlots;
            });

    public static Node? FindCardRewardScreen() {
        var stack = NOverlayStack.Instance;
        if (stack == null) return null;

        if (stack.Peek() is Node top) {
            if (top is NCardRewardSelectionScreen or NDeckCardSelectScreen or NChooseACardSelectionScreen)
                return top;

            return GameUi.FindFirst<NChooseACardSelectionScreen>(top)
                   ?? GameUi.FindFirst<NCardRewardSelectionScreen>(top)
                   ?? GameUi.FindFirst<NDeckCardSelectScreen>(top) as Node;
        }

        return null;
    }

    public static NChooseARelicSelection? FindRelicSelectionScreen() {
        if (NOverlayStack.Instance?.Peek() is NChooseARelicSelection direct)
            return direct;

        if (NOverlayStack.Instance?.Peek() is Node top)
            return GameUi.FindFirst<NChooseARelicSelection>(top);

        return null;
    }
}
