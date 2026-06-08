using KitLib.Panels;
using KitLib.UI;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions.CardModes;

internal sealed class AddModeHandler : ICardModeHandler {
    public string Id => "add";
    public bool ShowTargets => true;
    public bool ShowDuration => true;
    public bool RefreshOnTargetChange => false;

    public bool HasRelevantCards(Player player, CardTarget target)
        => target == CardTarget.Deck || player.PlayerCombatState != null;

    public void Execute(NGlobalUi globalUi, DevPanelActionSession session, RunState state, Player player) {
        CardBrowserUI.Show(globalUi, state, player);
    }

    public bool TryHandleCardSelection(NGlobalUi globalUi, NCardHolder holder,
                                       RunState state, Player player) => false;

    public void OnLibraryClosed(NGlobalUi globalUi) { }
}
