using System;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using KitLib.Panels;

namespace KitLib.Actions.CardModes;

/// <summary>
/// Strategy interface for card panel modes (View, Add, Upgrade, Delete, Edit).
/// Each handler encapsulates the mode-specific behavior that was previously
/// spread across DevPanel's switch/if chains.
/// </summary>
internal interface ICardModeHandler {
    string Id { get; }

    bool ShowTargets { get; }
    bool ShowDuration { get; }
    bool RefreshOnTargetChange { get; }

    bool HasRelevantCards(Player player, CardTarget target);

    void Execute(NGlobalUi globalUi, DevPanelActionSession session, RunState state, Player player);

    /// <returns>true if the selection was handled (suppress default behavior).</returns>
    bool TryHandleCardSelection(NGlobalUi globalUi, NCardHolder holder,
                                RunState state, Player player);

    void OnLibraryClosed(NGlobalUi globalUi);
}
