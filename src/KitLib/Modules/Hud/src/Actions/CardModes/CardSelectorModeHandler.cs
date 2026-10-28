using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KitLib.Navigation;
using KitLib.Panels;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions.CardModes;

/// <summary>
/// Deprecated: Previously used by Upgrade and Delete mode handlers to show
/// NDeckCardSelectScreen. Now replaced by CardBrowserUI's self-drawn grid.
/// Kept for potential external usage.
/// </summary>
[Obsolete("Upgrade/Delete handlers now use CardBrowserUI directly")]
internal abstract class CardSelectorModeHandler : ICardModeHandler {
    public abstract string Id { get; }
    public bool ShowTargets => true;
    public bool ShowDuration => true;
    public bool RefreshOnTargetChange => true;

    public virtual bool HasRelevantCards(Player player, CardTarget target) {
        var cards = CardActions.GetCardsForTarget(player, target);
        return FilterCards(cards).Count > 0;
    }

    public void Execute(NGlobalUi globalUi, DevPanelActionSession session, RunState state, Player player) {
        NavigationHelper.ClosePauseMenu();
        session.Run(
            () => ExecuteAsync(state, player),
            Id,
            onCompleted: DevPanel.ResetPanel
        );
    }

    public bool TryHandleCardSelection(NGlobalUi globalUi, NCardHolder holder,
                                       RunState state, Player player) => false;

    public void OnLibraryClosed(NGlobalUi globalUi) { }

    protected abstract List<CardModel> FilterCards(List<CardModel> cards);
    protected abstract Task ExecuteAsync(RunState state, Player player);
}
