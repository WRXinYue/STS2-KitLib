using System;
using System.Threading.Tasks;
using DevMode;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace DevMode.AI.Sts2.Helpers;

/// <summary>Waits for <see cref="CardModel.TryManualPlay"/> to finish via the action queue.</summary>
internal static class Sts2CombatPlayHelper {
    public static async Task<bool> WaitForManualPlayAsync(CardModel card, TimeSpan timeout) {
        if (NGame.Instance == null)
            return false;

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline) {
            await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);

            if (IsPlayStable(card, out _, out _))
                return DescribeOverlay() == null;

            if (DescribeOverlay() != null)
                return false;
        }

        return IsPlayStable(card, out _, out _) && DescribeOverlay() == null;
    }

    static bool IsPlayStable(CardModel card, out bool inHand, out bool settled) {
        inHand = IsCardInHand(card);
        settled = Sts2WaitHelper.ArePlayerDrivenActionsSettled();

        if (!CombatManager.Instance.IsInProgress)
            return true;

        if (inHand)
            return false;

        // Instant/skip-anim mode completes card logic without waiting on animation-driven action cleanup.
        if (SkipAnimControl.IsSkipping)
            return true;

        return settled;
    }

    static bool IsCardInHand(CardModel card) {
        if (RunContext.TryGetRunAndPlayer(out _, out var player)) {
            var hand = player.PlayerCombatState?.Hand?.Cards;
            if (hand != null)
                return hand.Contains(card);
        }

        return card.Pile?.Type == PileType.Hand;
    }

    static string? DescribeOverlay() {
        var peek = NOverlayStack.Instance?.Peek();
        return peek == null ? null : peek.GetType().Name;
    }
}
