using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.AutoPlay.Scoring;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.AI.Sts2.Helpers;

/// <summary>Resolves overlay UI that <see cref="NOverlayStack.Peek"/> alone can miss (nested / deck-select).</summary>
internal static class OverlayPhaseHelper {
    public static bool HasActiveCardRewardScreen() =>
        FindCardRewardScreen() != null;

    public static bool HasActiveRelicSelectionScreen() =>
        FindRelicSelectionScreen() != null;

    /// <summary>
    /// Terminal <see cref="NRewardsScreen"/> stays on the overlay stack after proceed opens the map.
    /// Treat as map selection once rewards are drained and the map is visible.
    /// </summary>
    public static bool RewardsReadyForMap(NRewardsScreen screen, Player? player, JsonObject? snapshot = null) {
        if (NMapScreen.Instance is not { IsOpen: true })
            return false;

        if (HasActiveCardRewardScreen())
            return false;

        if (screen.IsComplete)
            return true;

        // Combat uses terminal rewards (RewardsSet.Offer). ProceedFromTerminalRewardsScreen opens the map
        // without popping NRewardsScreen. Skipped card picks leave an enabled NRewardButton (RewardSkipped).
        if (RunManager.Instance?.DebugOnlyGetState()?.CurrentRoom is CombatRoom)
            return true;

        return !HasClickableRewards(screen, player?.HasOpenPotionSlots ?? false, snapshot);
    }

    public static bool HasClickableRewards(NRewardsScreen screen, bool hasPotionSlots, JsonObject? snapshot = null) =>
        UIHelper.FindAll<NRewardButton>((Node)screen)
            .Any(b => {
                if (!b.IsEnabled) return false;
                if (b.Reward is not PotionReward potionReward) return true;
                if (hasPotionSlots) return true;
                if (snapshot == null) return false;
                var id = potionReward.Potion?.Id.Entry;
                return PotionInventoryScorer.ShouldMakeRoom(id, snapshot, out _);
            });

    public static Node? FindCardRewardScreen() {
        var stack = NOverlayStack.Instance;
        if (stack == null) return null;

        if (stack.Peek() is Node top) {
            if (top is NCardRewardSelectionScreen or NDeckCardSelectScreen or NChooseACardSelectionScreen)
                return top;

            var nestedChoose = UIHelper.FindFirst<NChooseACardSelectionScreen>(top);
            if (nestedChoose != null)
                return nestedChoose;

            var nestedCard = UIHelper.FindFirst<NCardRewardSelectionScreen>(top);
            if (nestedCard != null)
                return nestedCard;

            var nestedDeck = UIHelper.FindFirst<NDeckCardSelectScreen>(top);
            if (nestedDeck != null)
                return nestedDeck;
        }

        return null;
    }

    public static NChooseARelicSelection? FindRelicSelectionScreen() {
        if (NOverlayStack.Instance?.Peek() is NChooseARelicSelection direct)
            return direct;

        if (NOverlayStack.Instance?.Peek() is Node top)
            return UIHelper.FindFirst<NChooseARelicSelection>(top);

        return null;
    }
}
