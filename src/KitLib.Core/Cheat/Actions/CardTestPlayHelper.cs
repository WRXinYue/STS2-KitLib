using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using KitLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions;

/// <summary>Combat helpers for card testing: hand cleanup, play completion waits, auto-choice.</summary>
internal static class CardTestPlayHelper {
    static Node? _pendingChoiceScreen;
    static DateTime? _choiceScreenReadyAt;
    static readonly HashSet<ulong> _freedCardNodeIds = new();

    const int DrawPileFillerCount = 7;

    /// <summary>
    /// Adds temporary starter Strikes to the draw pile so draw / discard / exhaust effects can resolve.
    /// Uses the current character's basic Strike (e.g. Fox Hime strike for Lust Travel 2).
    /// </summary>
    internal static async Task SeedDrawPileAsync(RunState state, Player player) {
        var filler = ResolveFillerCard(player);
        if (filler == null) {
            MainFile.Logger.Warn("CardTestActions: No filler card found for draw pile seeding.");
            return;
        }

        var id = ((AbstractModel)filler).Id.Entry;
        for (var i = 0; i < DrawPileFillerCount; i++) {
            await CardActions
                .Add(state, player, filler)
                .Target(CardTarget.DrawPile)
                .Duration(EffectDuration.Temporary)
                .RunAsync();
        }

        MainFile.Logger.Info($"CardTestActions: Seeded {DrawPileFillerCount}x {id} into draw pile.");
    }

    /// <summary>Character starter Strike — simple 1-cost attack with no on-draw or on-play side effects.</summary>
    static CardModel ResolveFillerCard(Player player) {
        var starting = player.Character?.StartingDeck;
        if (starting != null) {
            var strike = starting.FirstOrDefault(c => c.Tags.Contains(CardTag.Strike));
            if (strike != null)
                return strike;

            var attack = starting.FirstOrDefault(c => c.Type == CardType.Attack);
            if (attack != null)
                return attack;

            var any = starting.FirstOrDefault();
            if (any != null)
                return any;
        }

        return ModelDb.Card<StrikeIronclad>();
    }

    /// <summary>
    /// Removes every card from combat piles and syncs hand/table visuals.
    /// </summary>
    internal static async Task ClearCombatCards(Player player) {
        var pcs = player.PlayerCombatState;
        var combatState = player.Creature?.CombatState;
        if (pcs == null)
            return;

        await WaitForCombatSettledAsync();
        await WaitForCardVisualsIdleAsync();

        _freedCardNodeIds.Clear();
        NCombatRoom.Instance?.Ui.Hand.CancelAllCardPlay();
        KillAllCombatCardTweens();

        var cards = pcs.AllCards.ToList();
        foreach (var card in cards) {
            DestroyCardNode(card);
            if (card.Pile != null)
                card.RemoveFromCurrentPile(silent: true);
            if (combatState != null && combatState.ContainsCard(card))
                combatState.RemoveCard(card);
            card.HasBeenRemovedFromState = true;
        }

        foreach (var pile in pcs.AllPiles)
            pile.InvokeContentsChanged();

        await YieldFrameAsync();
        await YieldFrameAsync();
        SweepOrphanCardNodes();
        _freedCardNodeIds.Clear();

        if (cards.Count > 0)
            MainFile.Logger.Info($"CardTestActions: Cleared {cards.Count} combat card(s).");
        await WaitForCombatSettledAsync();
    }

    static async Task YieldFrameAsync() {
        if (NGame.Instance != null)
            await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
        else
            await Task.Delay(16);
    }

    static void DestroyCardNode(CardModel card) {
        var ui = NCombatRoom.Instance?.Ui;
        if (ui == null)
            return;

        var holder = ui.Hand.GetCardHolder(card);
        if (holder != null) {
            var node = holder.CardNode;
            KillCardVisualTween(node);
            try {
                ui.Hand.Remove(card);
            }
            catch {
                ui.Hand.RemoveCardHolder(holder);
            }
            ReturnCardNodeToPool(node);
            return;
        }

        var onTable = NCard.FindOnTable(card);
        if (onTable != null) {
            KillCardVisualTween(onTable);
            ReturnCardNodeToPool(onTable);
        }
    }

    static void KillCardVisualTween(NCard? node) {
        if (node == null || !GodotObject.IsInstanceValid(node))
            return;
        node.PlayPileTween?.Kill();
        node.PlayPileTween = null;
    }

    static void KillAllCombatCardTweens() {
        var ui = NCombatRoom.Instance?.Ui;
        if (ui == null)
            return;

        foreach (var node in FindAll<NCard>(ui))
            KillCardVisualTween(node);
    }

    /// <summary>
    /// NCards reparented onto <see cref="NCombatUi"/> during pile tweens are invisible to
    /// <see cref="NCard.FindOnTable"/> for draw/discard piles.
    /// </summary>
    static bool HasFloatingCombatCardVisuals() {
        var ui = NCombatRoom.Instance?.Ui;
        if (ui == null)
            return false;

        foreach (var node in FindAll<NCard>(ui)) {
            if (!GodotObject.IsInstanceValid(node) || !node.IsInsideTree())
                continue;

            if (node.PlayPileTween?.IsValid() == true && node.PlayPileTween.IsRunning())
                return true;

            if (ReferenceEquals(node.GetParent(), ui))
                return true;
        }

        return false;
    }

    static async Task WaitForCardVisualsIdleAsync(TimeSpan? timeout = null) {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));
        while (DateTime.UtcNow < deadline) {
            if (!HasFloatingCombatCardVisuals())
                return;
            await YieldFrameAsync();
        }

        KillAllCombatCardTweens();
    }

    static void ReturnCardNodeToPool(NCard? node) {
        if (node == null || !GodotObject.IsInstanceValid(node))
            return;
        if (!_freedCardNodeIds.Add(node.GetInstanceId()))
            return;
        node.QueueFreeSafely();
    }

    /// <summary>
    /// Recycles every NCard under combat UI — catches pile-tween orphans that outlive their models.
    /// </summary>
    static void SweepOrphanCardNodes() {
        var ui = NCombatRoom.Instance?.Ui;
        if (ui == null)
            return;

        foreach (var node in FindAll<NCard>(ui).ToList()) {
            KillCardVisualTween(node);
            ReturnCardNodeToPool(node);
        }

        SweepOrphanedHandNodes();
    }

    /// <summary>Removes hand holders whose card nodes were already destroyed.</summary>
    static void SweepOrphanedHandNodes() {
        var hand = NPlayerHand.Instance;
        if (hand == null)
            return;

        foreach (var holder in hand.ActiveHolders.ToList())
            hand.RemoveCardHolder(holder);

        hand.ForceRefreshCardIndices();
    }

    internal static async Task WaitForCombatSettledAsync(TimeSpan? timeout = null) {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(3));
        while (DateTime.UtcNow < deadline) {
            if (AreActionsSettled())
                return;

            if (NGame.Instance != null)
                await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
            else
                await Task.Delay(16);
        }
    }

    internal static async Task<bool> WaitForPlayAsync(CardModel card, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline) {
            await TryAutoResolveChoicesAsync();

            if (NGame.Instance != null)
                await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
            else
                await Task.Delay(16);

            if (IsPlayComplete(card))
                return true;
        }

        await TryAutoResolveChoicesAsync();
        return IsPlayComplete(card);
    }

    static bool IsPlayComplete(CardModel card) {
        if (!CombatManager.Instance.IsInProgress)
            return true;

        if (IsCardInHand(card))
            return false;

        return AreActionsSettled() && !HasFloatingCombatCardVisuals();
    }

    static bool IsCardInHand(CardModel card) => card.Pile?.Type == PileType.Hand;

    static bool AreActionsSettled() {
        var rm = RunManager.Instance;
        if (rm == null)
            return true;

        var running = rm.ActionExecutor.CurrentlyRunningAction;
        if (running != null && ActionQueueSet.IsGameActionPlayerDriven(running))
            return false;

        var ready = rm.ActionQueueSet.GetReadyAction();
        if (ready != null && ActionQueueSet.IsGameActionPlayerDriven(ready))
            return false;

        return true;
    }

    static async Task TryAutoResolveChoicesAsync() {
        if (!CardTestState.TestingActive)
            return;

        var hand = NPlayerHand.Instance;
        if (hand is { IsInCardSelection: true }) {
            await TryAutoResolveHandSelectionAsync(hand);
            return;
        }

        var screen = FindChoiceScreen();
        if (screen == null) {
            _pendingChoiceScreen = null;
            _choiceScreenReadyAt = null;
            return;
        }

        if (!ReferenceEquals(screen, _pendingChoiceScreen)) {
            _pendingChoiceScreen = screen;
            _choiceScreenReadyAt = DateTime.UtcNow.AddMilliseconds(400);
            return;
        }

        if (_choiceScreenReadyAt > DateTime.UtcNow)
            return;

        switch (screen) {
            case NChooseACardSelectionScreen choose:
                await AutoPickCardHolder(choose, "choose-a-card");
                break;
            case NCombatPileCardSelectScreen combatPile:
                await AutoPickGridSelect(combatPile, "combat-pile");
                break;
            case NSimpleCardSelectScreen simple:
                await AutoPickGridSelect(simple, "simple-card");
                break;
            case NDeckCardSelectScreen deck:
                await AutoPickGridSelect(deck, "deck-card");
                break;
        }
    }

    static async Task TryAutoResolveHandSelectionAsync(NPlayerHand hand) {
        if (!ReferenceEquals(hand, _pendingChoiceScreen)) {
            _pendingChoiceScreen = hand;
            _choiceScreenReadyAt = DateTime.UtcNow.AddMilliseconds(400);
            return;
        }

        if (_choiceScreenReadyAt > DateTime.UtcNow)
            return;

        var holders = OrderHoldersForTest(hand.ActiveHolders
            .Where(h => GodotObject.IsInstanceValid(h) && h.Visible && h.CardModel != null)
            .Cast<NCardHolder>()
            .ToList());
        if (holders.Count == 0)
            return;

        var confirm = hand.GetNodeOrNull<NConfirmButton>("%SelectModeConfirmButton");
        var pickCount = Math.Min(holders.Count, 3);
        for (var i = 0; i < pickCount; i++) {
            holders[i].EmitSignal(NCardHolder.SignalName.Pressed, holders[i]);
            await Task.Delay(40);
            if (confirm is { IsEnabled: true })
                break;
        }

        if (confirm is { IsEnabled: true }) {
            confirm.ForceClick();
            MainFile.Logger.Info("CardTestActions: Auto-confirmed hand card selection.");
        }
        else {
            MainFile.Logger.Info($"CardTestActions: Auto-selected hand card(s), awaiting confirm.");
        }

        _pendingChoiceScreen = null;
        _choiceScreenReadyAt = null;
        await Task.Delay(50);
    }

    static async Task AutoPickCardHolder(Node screen, string label) {
        var holder = PickBestHolder(screen);
        if (holder == null)
            return;

        holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
        MainFile.Logger.Info($"CardTestActions: Auto-selected first {label} option.");
        _pendingChoiceScreen = null;
        _choiceScreenReadyAt = null;
        await Task.Delay(50);
    }

    static async Task AutoPickGridSelect(Node screen, string label) {
        var holders = OrderHoldersForTest(FindAll<NCardHolder>(screen)
            .Where(h => GodotObject.IsInstanceValid(h) && h.Visible && h.CardModel != null)
            .ToList());
        if (holders.Count == 0)
            return;

        // Pick up to 3 cards to satisfy multi-select prompts (exhaust N, etc.).
        var pickCount = Math.Min(holders.Count, 3);
        for (var i = 0; i < pickCount; i++) {
            holders[i].EmitSignal(NCardHolder.SignalName.Pressed, holders[i]);
            await Task.Delay(40);
        }

        var confirm = (NClickableControl?)FindFirst<NConfirmButton>(screen)
                      ?? FindFirst<NProceedButton>(screen);
        if (confirm is { IsEnabled: true }) {
            confirm.ForceClick();
            MainFile.Logger.Info($"CardTestActions: Auto-confirmed {label} selection.");
        }
        else {
            MainFile.Logger.Info($"CardTestActions: Auto-selected {pickCount} card(s) for {label}.");
        }

        _pendingChoiceScreen = null;
        _choiceScreenReadyAt = null;
        await Task.Delay(50);
    }

    static NCardHolder? PickBestHolder(Node screen) {
        var holders = OrderHoldersForTest(FindAll<NCardHolder>(screen)
            .Where(h => GodotObject.IsInstanceValid(h) && h.Visible && h.CardModel != null)
            .ToList());
        return holders.FirstOrDefault();
    }

    static List<NCardHolder> OrderHoldersForTest(List<NCardHolder> holders) {
        var active = CardTestState.ActiveTestCard;
        if (active == null || holders.Count == 0)
            return holders;

        var activeId = ((AbstractModel)active).Id.Entry;
        var match = holders.FirstOrDefault(h => ReferenceEquals(h.CardModel, active))
                    ?? holders.FirstOrDefault(h =>
                        h.CardModel != null && ((AbstractModel)h.CardModel).Id.Entry == activeId);
        if (match == null)
            return holders;

        var ordered = new List<NCardHolder> { match };
        ordered.AddRange(holders.Where(h => !ReferenceEquals(h, match)));
        return ordered;
    }

    static Node? FindChoiceScreen() {
        var stack = NOverlayStack.Instance;
        if (stack?.Peek() is not Node top)
            return null;

        if (top is NChooseACardSelectionScreen
            or NDeckCardSelectScreen
            or NCombatPileCardSelectScreen
            or NSimpleCardSelectScreen
            or NCardGridSelectionScreen)
            return top;

        return FindFirst<NChooseACardSelectionScreen>(top)
               ?? FindFirst<NCombatPileCardSelectScreen>(top)
               ?? FindFirst<NSimpleCardSelectScreen>(top)
               ?? FindFirst<NDeckCardSelectScreen>(top)
               ?? FindFirst<NCardGridSelectionScreen>(top) as Node;
    }

    static List<T> FindAll<T>(Node start) where T : Node {
        var list = new List<T>();
        if (GodotObject.IsInstanceValid(start))
            FindAllRecursive(start, list);
        return list;
    }

    static T? FindFirst<T>(Node start) where T : Node {
        if (!GodotObject.IsInstanceValid(start))
            return null;
        if (start is T match)
            return match;
        foreach (var child in start.GetChildren()) {
            var found = FindFirst<T>(child);
            if (found != null)
                return found;
        }
        return null;
    }

    static void FindAllRecursive<T>(Node node, List<T> found) where T : Node {
        if (!GodotObject.IsInstanceValid(node))
            return;
        if (node is T item)
            found.Add(item);
        foreach (var child in node.GetChildren())
            FindAllRecursive(child, found);
    }
}
