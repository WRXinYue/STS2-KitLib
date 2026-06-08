using KitLib.Panels;
using KitLib.UI;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions.CardModes;

internal sealed class ViewModeHandler : ICardModeHandler {
    public string Id => "view";
    public bool ShowTargets => false;
    public bool ShowDuration => false;
    public bool RefreshOnTargetChange => false;

    public bool HasRelevantCards(Player player, CardTarget target) => true;

    public void Execute(NGlobalUi globalUi, DevPanelActionSession session, RunState state, Player player) {
        CardBrowserUI.Show(globalUi, state, player);
    }

    public bool TryHandleCardSelection(NGlobalUi globalUi, NCardHolder holder,
                                       RunState state, Player player) => false;

    public void OnLibraryClosed(NGlobalUi globalUi) { }
}
