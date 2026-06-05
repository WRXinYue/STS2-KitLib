using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;

namespace DevMode.AI.Sts2.Helpers;

/// <summary>Resolves overlay UI that <see cref="NOverlayStack.Peek"/> alone can miss (nested / deck-select).</summary>
internal static class OverlayPhaseHelper {
    public static bool HasActiveCardRewardScreen() =>
        FindCardRewardScreen() != null;

    public static bool HasActiveRelicSelectionScreen() =>
        FindRelicSelectionScreen() != null;

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
