using System;
using System.Threading.Tasks;
using Godot;
using KitLib;
using KitLib.AI.Sts2.Mcp;
using KitLib.Host;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace KitLib.AI.Sts2.Helpers;

/// <summary>Waits for <see cref="CardModel.TryManualPlay"/> to finish via the action queue.</summary>
internal static class Sts2CombatPlayHelper {
    public static async Task<ManualPlayOutcome> WaitForManualPlayAsync(CardModel card, TimeSpan timeout) {
        if (NGame.Instance == null)
            return ManualPlayOutcome.Failed;

        var deadline = DateTime.UtcNow + timeout;
        var autoSelect = !string.IsNullOrWhiteSpace(McpPlayContext.SelectionCardId)
                         || McpPlayContext.SelectionIndex.HasValue;
        var autoSelectAttempted = false;

        while (DateTime.UtcNow < deadline) {
            await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);

            if (McpCardSelectionHelper.IsActive()) {
                if (autoSelect && !autoSelectAttempted) {
                    autoSelectAttempted = true;
                    await McpCardSelectionHelper.TryAutoPickAsync(
                        McpPlayContext.SelectionCardId,
                        McpPlayContext.SelectionIndex,
                        TimeSpan.FromSeconds(1));
                }
                continue;
            }

            if (IsPlayStable(card, out _, out _)) {
                if (!HasBlockingOverlay())
                    return ManualPlayOutcome.Completed;
            }
        }

        if (McpCardSelectionHelper.IsActive())
            return ManualPlayOutcome.PendingSelection;

        return IsPlayStable(card, out _, out _) && !HasBlockingOverlay()
            ? ManualPlayOutcome.Completed
            : ManualPlayOutcome.Failed;
    }

    static bool HasBlockingOverlay() {
        if (NPlayerHand.Instance is { IsInCardSelection: true })
            return true;

        var peek = NOverlayStack.Instance?.Peek();
        if (peek == null)
            return false;

        return peek is not (
            NCombatPileCardSelectScreen
            or NSimpleCardSelectScreen
            or NChooseACardSelectionScreen
            or NDeckCardSelectScreen
            or NCardGridSelectionScreen);
    }

    static bool IsPlayStable(CardModel card, out bool inHand, out bool settled) {
        inHand = IsCardInHand(card);
        settled = Sts2WaitHelper.ArePlayerDrivenActionsSettled();

        if (!CombatManager.Instance.IsInProgress)
            return true;

        if (inHand)
            return false;

        if (KitLibCheatOps.IsSkipAnimSkipping?.Invoke() == true)
            return true;

        return settled;
    }

    static bool IsCardInHand(CardModel card) {
        if (RunContext.TryGetRunAndPlayer(out _, out var player)) {
            var hand = player.PlayerCombatState?.Hand?.Cards;
            if (hand != null) {
                foreach (var c in hand) {
                    if (ReferenceEquals(c, card))
                        return true;
                }

                return false;
            }
        }

        return card.Pile?.Type == PileType.Hand;
    }
}

internal enum ManualPlayOutcome {
    Completed,
    PendingSelection,
    Failed,
}
