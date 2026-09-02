using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using KitLib.Actions;
using KitLib.AI.Core.Schema;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Game;

/// <summary>Maps <see cref="GameAction"/> to STS2 UI / player commands.</summary>
internal sealed class PlayerActionExecutor {
    private readonly Action<string> _log;
    private readonly HashSet<NRewardButton> _attemptedRewardButtons = new();
    private readonly HashSet<NRewardButton> _declinedRewardButtons = new();
    private NRewardButton? _pendingCardRewardButton;
    private NRewardsScreen? _lastRewardScreen;
    private bool _cardRewardDeclined;

    public PlayerActionExecutor(Action<string> log) {
        _log = log;
    }

    public async Task<ActionResult> ExecuteAsync(
        RunState state,
        Player player,
        GameAction action,
        SelectionHint? hint) {
        return action.Type switch {
            ActionType.PlayCard => await PlayCard(player, action.TargetIndex, action.SecondaryIndex, hint),
            ActionType.EndTurn => EndTurn(player),
            ActionType.SelectMapNode => await SelectMapNode(state, action.TargetIndex),
            ActionType.PickCardReward => await PickCardReward(action.TargetIndex),
            ActionType.SkipCardReward => await SkipCardReward(),
            ActionType.SelectEventChoice => await SelectEventChoice(action.TargetIndex),
            ActionType.PurchaseShopItem => await PurchaseShopItem(action.TargetIndex),
            ActionType.RemoveCardAtShop => await RemoveCardAtShop(),
            ActionType.LeaveShop => await LeaveShop(),
            ActionType.Rest => await SelectRestSiteOption(action.TargetIndex),
            ActionType.UpgradeCard => await SelectRestSiteOption(action.TargetIndex),
            ActionType.UsePotion => await UsePotionAsync(player, action.TargetIndex, action.SecondaryIndex),
            ActionType.DiscardPotion => await DiscardPotion(player, action.TargetIndex),
            ActionType.CollectReward => await CollectReward(player),
            ActionType.DismissRewards => await DismissRewards(),
            ActionType.Proceed => await Proceed(state),
            ActionType.HandleTreasureRoom => await HandleTreasureRoom(),
            ActionType.PickRelic => await PickRelic(action.TargetIndex),
            ActionType.AdvanceOverlay => await AdvanceOverlay(),
            ActionType.PressConfirm => await Proceed(state),
            ActionType.Wait => ActionResult.Ok("Waiting."),
            _ => ActionResult.Fail($"Unknown action type: {action.Type}"),
        };
    }

    async Task<ActionResult> PlayCard(Player player, int cardIndex, int targetIndex, SelectionHint? hint) {
        var combatState = player.PlayerCombatState;
        if (combatState == null) return ActionResult.Fail("Not in combat.");

        if (!Sts2CombatCompat.IsCombatPlayPhaseActive())
            return ActionResult.Fail("Not in play phase.");

        var hand = combatState.Hand?.Cards.ToList();
        if (hand == null || cardIndex < 0 || cardIndex >= hand.Count)
            return ActionResult.Fail($"Invalid card index: {cardIndex} (hand size: {hand?.Count ?? 0})");

        var card = hand[cardIndex];
        var target = ResolveCardTarget(player, card, targetIndex);

        if (target == null && card.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly)
            return ActionResult.Fail($"Card [{card.Title}] needs a target but none was resolved.");

        if (!card.CanPlayTargeting(target))
            return ActionResult.Fail($"Card [{card.Title}] cannot be played.");

        if (!card.TryManualPlay(target))
            return ActionResult.Fail($"Card [{card.Title}] cannot be played.");

        var outcome = await CombatPlayWait.WaitForManualPlayAsync(card, hint, TimeSpan.FromSeconds(30));
        return outcome switch {
            ManualPlayOutcome.Completed => ActionResult.Ok($"Played [{card.Title}]"),
            ManualPlayOutcome.PendingSelection => ActionResult.Fail("pending_selection"),
            _ => ActionResult.Fail($"Card [{card.Title}] play did not complete."),
        };
    }

    static ActionResult EndTurn(Player player) {
        var combatState = player.PlayerCombatState;
        if (combatState == null) return ActionResult.Fail("Not in combat.");

        if (!Sts2CombatCompat.IsCombatPlayPhaseActive())
            return ActionResult.Fail("Not in play phase.");

        PlayerCmd.EndTurn(player, canBackOut: false);
        return ActionResult.Ok("Turn ended.");
    }

    static Creature? ResolveCardTarget(Player player, CardModel card, int targetIndex) {
        var combatState = player.Creature.CombatState;
        if (combatState == null) return null;

        switch (card.TargetType) {
            case TargetType.AnyEnemy: {
                    if (combatState.HittableEnemies.Any() == false)
                        return null;
                    return CombatTargetResolver.ResolveEnemy((CombatState)combatState, card, targetIndex);
                }
            case TargetType.AnyAlly: {
                    var allies = combatState.PlayerCreatures.Where(c => c.IsAlive);
                    return allies.FirstOrDefault(card.IsValidTarget) ?? player.Creature;
                }
            case TargetType.AnyPlayer:
            case TargetType.Self:
                return null;
            default:
                return null;
        }
    }

    async Task<ActionResult> SelectMapNode(RunState state, int nodeIndex) {
        var mapScreen = NMapScreen.Instance;
        if (mapScreen == null || !mapScreen.IsOpen)
            return ActionResult.Fail("Map screen not open.");

        var allPoints = GameUi.FindAll<NMapPoint>((Node)mapScreen);

        List<NMapPoint> available;
        if (state.VisitedMapCoords.Count == 0) {
            available = allPoints.Where(mp => mp.Point.coord.row == 0).ToList();
        }
        else {
            var visited = state.VisitedMapCoords;
            var lastCoord = visited[visited.Count - 1];
            var lastPoint = allPoints.FirstOrDefault(mp => mp.Point.coord.Equals(lastCoord));
            if (lastPoint == null) return ActionResult.Fail("Cannot find current position on map.");

            var childCoords = new HashSet<MapCoord>(lastPoint.Point.Children.Select(c => c.coord));
            available = allPoints.Where(mp => childCoords.Contains(mp.Point.coord)).ToList();
        }

        if (available.Count == 0) return ActionResult.Fail("No available map nodes.");

        var idx = nodeIndex >= 0 && nodeIndex < available.Count ? nodeIndex : 0;
        var target = available[idx];

        await GameUi.Click(target);
        return ActionResult.Ok($"Selected map node at ({target.Point.coord.row}, {target.Point.coord.col})");
    }

    async Task<ActionResult> Proceed(RunState state) {
        if (NOverlayStack.Instance?.Peek() is Node overlayNode) {
            var btn = GameUi.FindFirst<NProceedButton>(overlayNode);
            if (btn is { IsEnabled: true }) {
                await GameUi.Click(btn);
                return ActionResult.Ok("Proceeded from overlay.");
            }
        }

        var root = ((SceneTree)Engine.GetMainLoop()).Root;

        var restRoom = root.GetNodeOrNull<NRestSiteRoom>(
            "/root/Game/RootSceneContainer/Run/RoomContainer/RestSiteRoom");
        if (restRoom?.ProceedButton is { IsEnabled: true } restProceed) {
            await GameUi.Click(restProceed);
            return ActionResult.Ok("Left rest site.");
        }

        var cm = CombatManager.Instance;
        var isPostCombatRoom = state.CurrentRoom?.RoomType is RoomType.Monster or RoomType.Elite or RoomType.Boss;
        if (isPostCombatRoom && cm != null && !cm.IsInProgress) {
            bool appeared = await GameUi.WaitUntil(() =>
                NOverlayStack.Instance?.Peek() is NRewardsScreen
                || (NMapScreen.Instance?.IsOpen ?? false),
                TimeSpan.FromSeconds(10));

            return appeared
                ? ActionResult.Ok("Rewards screen or map appeared after combat.")
                : ActionResult.Fail("Timed out waiting for post-combat screen.");
        }

        var roomContainer = root.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer");
        if (roomContainer != null) {
            var btn = GameUi.FindFirst<NProceedButton>(roomContainer);
            if (btn is { IsEnabled: true }) {
                await GameUi.Click(btn);
                return ActionResult.Ok("Proceeded from room.");
            }
        }

        return ActionResult.Fail("No proceed button found.");
    }

    async Task<ActionResult> PickCardReward(int cardIndex) {
        var screen = GameOverlayPhase.FindCardRewardScreen();
        if (screen == null)
            return ActionResult.Fail("Card reward screen not open.");

        if (screen is NChooseACardSelectionScreen chooseScreen) {
            var holders = GameUi.FindAll<NCardHolder>((Node)chooseScreen)
                .Where(h => GodotObject.IsInstanceValid(h) && h.Visible && h.CardModel != null)
                .ToList();
            if (holders.Count == 0) return ActionResult.Fail("No choose-a-card options found.");

            var idx = cardIndex >= 0 && cardIndex < holders.Count ? cardIndex : 0;
            await Task.Delay(400);
            holders[idx].EmitSignal(NCardHolder.SignalName.Pressed, holders[idx]);
            return ActionResult.Ok($"Picked choose-a-card option {idx}.");
        }

        if (screen is NCardRewardSelectionScreen cardRewardScreen) {
            var holders = GameUi.FindAll<NCardHolder>((Node)cardRewardScreen);
            if (holders.Count == 0) return ActionResult.Fail("No card rewards found.");

            var idx = cardIndex >= 0 && cardIndex < holders.Count ? cardIndex : 0;
            holders[idx].EmitSignal(NCardHolder.SignalName.Pressed, holders[idx]);
            if (!await GameUi.WaitUntil(
                () => !GameOverlayPhase.HasActiveCardRewardScreen(),
                TimeSpan.FromSeconds(10)))
                return ActionResult.Fail("Card reward screen did not close after pick.");

            _pendingCardRewardButton = null;
            return ActionResult.Ok($"Picked card reward {idx}.");
        }

        if (screen is NDeckCardSelectScreen deckScreen) {
            var holders = GameUi.FindAll<NCardHolder>(deckScreen)
                .Where(h => GodotObject.IsInstanceValid(h) && h.Visible)
                .ToList();
            if (holders.Count == 0) return ActionResult.Fail("No deck card choices found.");

            var idx = cardIndex >= 0 && cardIndex < holders.Count ? cardIndex : 0;
            var holder = holders[idx];
            holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);

            var confirm = GameUi.FindFirst<NProceedButton>(deckScreen);
            if (confirm is { IsEnabled: true }) {
                await GameUi.Click(confirm);
                return ActionResult.Ok($"Confirmed deck card choice {idx}.");
            }

            return ActionResult.Ok($"Selected deck card {idx} (awaiting confirm).");
        }

        return ActionResult.Fail($"Unsupported card reward screen: {screen.GetType().Name}");
    }

    async Task<ActionResult> SkipCardReward() {
        var screen = GameOverlayPhase.FindCardRewardScreen();
        if (screen == null)
            return ActionResult.Fail("Card reward screen not open.");

        if (screen is NChooseACardSelectionScreen chooseScreen) {
            var skip = GameUi.FindFirst<NChoiceSelectionSkipButton>(chooseScreen);
            if (skip is { Visible: true, IsEnabled: true }) {
                await GameUi.Click(skip);
                return ActionResult.Ok("Skipped choose-a-card screen.");
            }
        }

        if (screen is NDeckCardSelectScreen deckScreen) {
            var deckBack = GameUi.FindFirst<NBackButton>(deckScreen);
            if (deckBack != null) {
                await GameUi.Click(deckBack);
                return ActionResult.Ok("Cancelled deck card selection.");
            }
        }

        if (screen is not NCardRewardSelectionScreen cardRewardScreen)
            return ActionResult.Fail("Card reward screen not open.");

        await GameWait.ActionsSettled(TimeSpan.FromSeconds(5));

        NCardRewardAlternativeButton? skipBtn = null;
        if (!await GameUi.WaitUntil(() => {
            skipBtn = GameUi.FindAll<NCardRewardAlternativeButton>((Node)cardRewardScreen)
                .FirstOrDefault(b => GodotObject.IsInstanceValid(b) && b.Visible && b.IsEnabled);
            return skipBtn != null;
        }, TimeSpan.FromSeconds(2)))
            return ActionResult.Fail("No skip button found on card reward screen.");

        await GameUi.Click(skipBtn!);

        if (!await GameUi.WaitUntil(
            () => !GameOverlayPhase.HasActiveCardRewardScreen(),
            TimeSpan.FromSeconds(10)))
            return ActionResult.Fail("Card reward screen did not close after skip.");

        await GameWait.ActionsSettled(TimeSpan.FromSeconds(5));

        if (_pendingCardRewardButton != null && GodotObject.IsInstanceValid(_pendingCardRewardButton)) {
            _declinedRewardButtons.Add(_pendingCardRewardButton);
            _attemptedRewardButtons.Add(_pendingCardRewardButton);
        }

        _cardRewardDeclined = true;
        _pendingCardRewardButton = null;
        return ActionResult.Ok("Skipped card reward.");
    }

    async Task<ActionResult> CollectReward(Player player) {
        if (NOverlayStack.Instance?.Peek() is not NRewardsScreen screen) {
            ResetRewardTracking();
            return ActionResult.Fail("Rewards screen not open.");
        }

        if (_lastRewardScreen != screen) {
            ResetRewardTracking();
            _lastRewardScreen = screen;
        }

        if (GameOverlayPhase.RewardsReadyForMap(screen, player)) {
            ResetRewardTracking();
            return ActionResult.Ok("Rewards complete; map is open.");
        }

        var clicked = 0;
        while (clicked < 12) {
            var btn = GameUi.FindAll<NRewardButton>((Node)screen)
                .FirstOrDefault(b => b.IsEnabled
                    && !_attemptedRewardButtons.Contains(b)
                    && !_declinedRewardButtons.Contains(b)
                    && !(_cardRewardDeclined && b.Reward is CardReward));

            if (btn == null)
                break;

            if (btn.Reward is PotionReward && !player.HasOpenPotionSlots) {
                _attemptedRewardButtons.Add(btn);
                _log("CollectReward: skip potion (no open belt slot).");
                continue;
            }

            _attemptedRewardButtons.Add(btn);
            if (btn.Reward is CardReward)
                _pendingCardRewardButton = btn;

            var rewardKind = btn.Reward?.GetType().Name ?? "?";
            _log($"CollectReward: clicking [{rewardKind}] (attempted={_attemptedRewardButtons.Count})");
            await GameUi.Click(btn);
            clicked++;

            if (!await RewardClaimWait.WaitForClaimAsync(screen, btn, TimeSpan.FromSeconds(10)))
                return ActionResult.Fail("Timed out waiting for reward claim.");

            var top = NOverlayStack.Instance?.Peek();
            if (top != null && top != (IOverlayScreen)screen)
                return ActionResult.Ok("Child overlay opened.");
        }

        if (clicked > 0)
            return ActionResult.Ok($"Collected {clicked} reward(s).");

        if (GameOverlayPhase.RewardsReadyForMap(screen, player)) {
            ResetRewardTracking();
            return ActionResult.Ok("Rewards complete; map is open.");
        }

        var proceedBtn = GameUi.FindFirst<NProceedButton>((Node)screen);
        if (proceedBtn != null) {
            await GameUi.WaitUntil(
                () => proceedBtn.IsEnabled || screen.IsComplete,
                TimeSpan.FromSeconds(10));
            await GameUi.Click(proceedBtn);
            var settled = await GameUi.WaitUntil(
                () => !GodotObject.IsInstanceValid((Node)screen)
                      || NOverlayStack.Instance?.Peek() != (IOverlayScreen)screen
                      || (NMapScreen.Instance?.IsOpen ?? false),
                TimeSpan.FromSeconds(15));
            if (settled && GameOverlayPhase.RewardsReadyForMap(screen, player)) {
                ResetRewardTracking();
                return ActionResult.Ok("Proceed clicked.");
            }

            return ActionResult.Ok("Proceed clicked; awaiting map.");
        }

        return ActionResult.Fail("Rewards screen has no clickable buttons yet.");
    }

    void ResetRewardTracking() {
        _lastRewardScreen = null;
        _attemptedRewardButtons.Clear();
        _declinedRewardButtons.Clear();
        _pendingCardRewardButton = null;
        _cardRewardDeclined = false;
    }

    async Task<ActionResult> HandleTreasureRoom() {
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        var room = root.GetNodeOrNull<NTreasureRoom>(
            "/root/Game/RootSceneContainer/Run/RoomContainer/TreasureRoom");
        if (room == null)
            return ActionResult.Fail("TreasureRoom node not found.");

        var chest = room.GetNodeOrNull<NClickableControl>("Chest");
        if (chest != null && chest.IsEnabled) {
            await GameUi.Click(chest);
            await GameUi.WaitUntil(
                () => GameUi.FindAll<NTreasureRoomRelicHolder>((Node)room)
                    .Any(h => h.Visible && h.IsEnabled),
                TimeSpan.FromSeconds(5));
        }

        var relicHolders = GameUi.FindAll<NTreasureRoomRelicHolder>((Node)room);
        foreach (var holder in relicHolders) {
            if (holder.IsEnabled && holder.Visible) {
                await GameUi.Click(holder);
                await GameWait.ActionsSettled(TimeSpan.FromSeconds(5));
            }
        }

        var proceedBtn = room.ProceedButton;
        if (proceedBtn != null) {
            await GameUi.WaitUntil(() => proceedBtn.IsEnabled, TimeSpan.FromSeconds(5));
            if (proceedBtn.IsEnabled) {
                await GameUi.Click(proceedBtn);
                return ActionResult.Ok("Treasure room completed.");
            }
        }

        return ActionResult.Ok("TreasureRoom: waiting.");
    }

    async Task<ActionResult> DismissRewards() {
        if (NOverlayStack.Instance?.Peek() is not NRewardsScreen screen)
            return ActionResult.Fail("Rewards screen not open.");

        if (NMapScreen.Instance is { IsOpen: true } && screen.IsComplete)
            return ActionResult.Ok("Rewards complete; map is open.");

        var proceedBtn = GameUi.FindFirst<NProceedButton>((Node)screen);
        if (proceedBtn == null)
            return ActionResult.Fail("No proceed button on rewards.");

        await GameUi.WaitUntil(
            () => proceedBtn.IsEnabled || screen.IsComplete,
            TimeSpan.FromSeconds(10));
        await GameUi.Click(proceedBtn);
        await GameUi.WaitUntil(
            () => !GodotObject.IsInstanceValid((Node)screen)
                  || NOverlayStack.Instance?.Peek() != (IOverlayScreen)screen
                  || (NMapScreen.Instance?.IsOpen ?? false),
            TimeSpan.FromSeconds(10));
        return ActionResult.Ok("Dismissed rewards.");
    }

    async Task<ActionResult> PickRelic(int relicIndex) {
        var screen = GameOverlayPhase.FindRelicSelectionScreen();
        if (screen == null)
            return ActionResult.Fail("Relic selection screen not open.");

        var entries = GameUi.FindAll<NRelicCollectionEntry>((Node)screen)
            .Where(e => e.Visible).ToList();
        if (entries.Count > 0) {
            var idx = relicIndex >= 0 && relicIndex < entries.Count ? relicIndex : 0;
            var entry = entries[idx];
            if (entry is NClickableControl clickable)
                await GameUi.Click(clickable);
            else
                return ActionResult.Fail("Relic entry is not clickable.");
            return ActionResult.Ok($"Picked relic option {idx}.");
        }

        var holders = GameUi.FindAll<NTreasureRoomRelicHolder>((Node)screen)
            .Where(h => h.IsEnabled && h.Visible).ToList();
        if (holders.Count > 0) {
            var idx = relicIndex >= 0 && relicIndex < holders.Count ? relicIndex : 0;
            await GameUi.Click(holders[idx]);
            return ActionResult.Ok($"Picked relic holder {idx}.");
        }

        return ActionResult.Fail("No relic choices found.");
    }

    async Task<ActionResult> AdvanceOverlay() {
        if (GameOverlayPhase.FindCardRewardScreen() != null)
            return await PickCardReward(0);

        if (!RunContext.TryGetRunAndPlayer(out var state, out _))
            return ActionResult.Fail("No active run.");

        var proceed = await Proceed(state);
        if (proceed.Success) return proceed;

        if (NOverlayStack.Instance?.Peek() is Node overlay) {
            var back = GameUi.FindFirst<NBackButton>(overlay);
            if (back is { IsEnabled: true }) {
                await GameUi.Click(back);
                return ActionResult.Ok("Dismissed overlay via back.");
            }
        }

        return ActionResult.Fail("Could not advance overlay.");
    }

    async Task<ActionResult> SelectEventChoice(int choiceIndex) {
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        var eventRoom = root.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer/EventRoom");
        if (eventRoom == null) return ActionResult.Fail("Event room not found.");

        var options = GameUi.FindAll<NEventOptionButton>(eventRoom)
            .Where(o => !o.Option.IsLocked).ToList();
        if (options.Count == 0) return ActionResult.Fail("No event options available.");

        var idx = choiceIndex >= 0 && choiceIndex < options.Count ? choiceIndex : 0;
        await GameUi.Click(options[idx]);
        return ActionResult.Ok($"Selected event option {idx}.");
    }

    async Task<ActionResult> PurchaseShopItem(int itemIndex) {
        var room = FindMerchantRoom();
        if (room == null) return ActionResult.Fail("Shop not found.");

        var affordable = new List<NMerchantSlot>();
        foreach (var slot in room.Inventory!.GetAllSlots()) {
            if (!slot.Entry.IsStocked || !slot.Entry.EnoughGold) continue;
            if (slot is NMerchantCardRemoval) continue;
            affordable.Add(slot);
        }

        if (affordable.Count == 0) return ActionResult.Fail("No affordable items.");

        var idx = itemIndex >= 0 && itemIndex < affordable.Count ? itemIndex : 0;
        await affordable[idx].Entry.OnTryPurchaseWrapper(room.Inventory.Inventory);
        return ActionResult.Ok($"Purchased item (cost: {affordable[idx].Entry.Cost}).");
    }

    async Task<ActionResult> RemoveCardAtShop() {
        var room = FindMerchantRoom();
        if (room == null) return ActionResult.Fail("Shop not found.");

        var removalSlot = room.Inventory?.GetAllSlots()
            .OfType<NMerchantCardRemoval>()
            .FirstOrDefault(s => s.Entry.IsStocked && s.Entry.EnoughGold);

        if (removalSlot == null) return ActionResult.Fail("Card removal not available or too expensive.");

        await removalSlot.Entry.OnTryPurchaseWrapper(room.Inventory!.Inventory);
        return ActionResult.Ok("Initiated card removal.");
    }

    async Task<ActionResult> LeaveShop() {
        var room = FindMerchantRoom();
        if (room == null) return ActionResult.Fail("Shop not found.");

        var backBtn = GameUi.FindFirst<NBackButton>((Node)room);
        if (backBtn != null)
            await GameUi.Click(backBtn);

        await GameUi.WaitUntil(
            () => room.ProceedButton is { IsEnabled: true },
            TimeSpan.FromSeconds(5));

        if (room.ProceedButton is { IsEnabled: true })
            await GameUi.Click(room.ProceedButton);

        return ActionResult.Ok("Left shop.");
    }

    async Task<ActionResult> SelectRestSiteOption(int optionIndex) {
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        var room = root.GetNodeOrNull<NRestSiteRoom>(
            "/root/Game/RootSceneContainer/Run/RoomContainer/RestSiteRoom");
        if (room == null) return ActionResult.Fail("Rest site not found.");

        var options = room.Options;
        if (options.Count == 0) return ActionResult.Fail("No rest options available.");

        if (optionIndex < 0 || optionIndex >= options.Count)
            return ActionResult.Fail($"Invalid rest option index: {optionIndex}.");

        var option = options[optionIndex];
        if (!option.IsEnabled)
            return ActionResult.Fail($"Rest option {optionIndex} is disabled.");

        var button = room.GetButtonForOption(option);
        if (button == null)
            return ActionResult.Fail($"Rest option {optionIndex} has no button.");

        await GameUi.Click(button);

        await GameUi.WaitUntil(() =>
                NOverlayStack.Instance?.Peek() is NRewardsScreen
                || !option.IsEnabled
                || room.ProceedButton is { IsEnabled: true },
            TimeSpan.FromSeconds(8));

        return ActionResult.Ok($"Selected rest option: {option.GetType().Name}");
    }

    static async Task<ActionResult> UsePotionAsync(Player player, int potionSlot, int targetIndex) {
        var potion = player.GetPotionAtSlotIndex(potionSlot);
        if (potion == null)
            return ActionResult.Fail($"No potion in slot {potionSlot}.");

        var potionId = potion.Id.Entry ?? "";
        Creature? target = null;
        if (potion.TargetType.IsSingleTarget()) {
            var combatState = player.Creature.CombatState;
            if (combatState != null) {
                target = potion.TargetType == TargetType.AnyEnemy
                    ? CombatTargetResolver.ResolveHittableEnemy(
                        (CombatState)combatState, targetIndex >= 0 ? targetIndex : 0)
                    : combatState.PlayerCreatures.FirstOrDefault(c => c.IsAlive);
            }
        }

        potion.EnqueueManualUse(target);

        if (!await PotionUseWait.WaitForManualUseAsync(
                player, potionSlot, potionId, TimeSpan.FromSeconds(8)))
            return ActionResult.Fail($"Potion [{potionId}] use did not complete.");

        return ActionResult.Ok($"Used potion [{potionId}] slot {potionSlot}.");
    }

    static async Task<ActionResult> DiscardPotion(Player player, int potionSlot) {
        var potion = player.GetPotionAtSlotIndex(potionSlot);
        if (potion == null)
            return ActionResult.Fail($"No potion in slot {potionSlot}.");

        await PotionActions.DiscardPotion(potion);
        return ActionResult.Ok($"Discarded potion [{potion.Id.Entry}] from slot {potionSlot}.");
    }

    static NMerchantRoom? FindMerchantRoom() {
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        return root.GetNodeOrNull<NMerchantRoom>(
            "/root/Game/RootSceneContainer/Run/RoomContainer/MerchantRoom");
    }
}
