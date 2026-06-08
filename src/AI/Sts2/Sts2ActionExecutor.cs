using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using KitLib;
using KitLib.AI;
using KitLib.Actions;
using KitLib.Multiplayer.PseudoCoop;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using KitLib.AI.AutoPlay.Scoring;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.AI.Sts2.Helpers;

namespace KitLib.AI.Sts2;

/// <summary>
/// Maps <see cref="GameAction"/> to STS2 game API calls.
/// </summary>
public sealed class Sts2ActionExecutor : IGameActionExecutor
{
    private readonly Sts2StateProvider _stateProvider;
    private readonly Action<string> _log;

    // Reward screen tracking
    private readonly HashSet<NRewardButton> _attemptedRewardButtons = new();
    private readonly HashSet<NRewardButton> _declinedRewardButtons = new();
    private NRewardButton? _pendingCardRewardButton;
    private NRewardsScreen? _lastRewardScreen;
    private bool _cardRewardDeclined;

    public Sts2ActionExecutor(Sts2StateProvider stateProvider, Action<string> log)
    {
        _stateProvider = stateProvider;
        _log = log;
    }

    public async Task<ActionResult> ExecuteAsync(GameAction action)
    {
        if (!_stateProvider.TryGetRunAndPlayer(out var state, out var player))
            return ActionResult.Fail("No active run or player.");

        if (player.PlayerCombatState != null && action.Type is ActionType.PlayCard or ActionType.EndTurn or ActionType.UsePotion) {
            return action.Type switch {
                ActionType.PlayCard => await AiMainThread.InvokeAsync(() => PlayCard(player, action.TargetIndex, action.SecondaryIndex)),
                ActionType.EndTurn => await AiMainThread.InvokeAsync(() => Task.FromResult(EndTurn(player))),
                ActionType.UsePotion => await AiMainThread.InvokeAsync(() => UsePotionAsync(player, action.TargetIndex, action.SecondaryIndex)),
                _ => ActionResult.Fail($"Unexpected combat action: {action.Type}"),
            };
        }

        return action.Type switch
        {
            ActionType.PlayCard => await PlayCard(player, action.TargetIndex, action.SecondaryIndex),
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
            ActionType.CollectReward => await CollectReward(action.TargetIndex),
            ActionType.DismissRewards => await DismissRewards(),
            ActionType.Proceed => await Proceed(),
            ActionType.HandleTreasureRoom => await HandleTreasureRoom(),
            ActionType.PickRelic => await PickRelic(action.TargetIndex),
            ActionType.AdvanceOverlay => await AdvanceOverlay(),
            ActionType.PressConfirm => await Proceed(),
            ActionType.Wait => ActionResult.Ok("Waiting."),
            _ => ActionResult.Fail($"Unknown action type: {action.Type}"),
        };
    }

    // ── Combat ──

    private async Task<ActionResult> PlayCard(Player player, int cardIndex, int targetIndex)
    {
        var combatState = player.PlayerCombatState;
        if (combatState == null) return ActionResult.Fail("Not in combat.");

        if (!Sts2CombatCompat.IsCombatPlayPhaseActive())
            return ActionResult.Fail("Not in play phase.");

        if (LanAiOwnership.IsHostHandPlayLocal(player))
            return ActionResult.Fail("LAN host local combat is hand-play only.");

        var hand = combatState.Hand?.Cards.ToList();
        if (hand == null || cardIndex < 0 || cardIndex >= hand.Count)
            return ActionResult.Fail($"Invalid card index: {cardIndex} (hand size: {hand?.Count ?? 0})");

        var card = hand[cardIndex];
        var target = ResolveCardTarget(player, card, targetIndex);

        if (target == null && card.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly)
            return ActionResult.Fail($"Card [{card.Title}] needs a target but none was resolved.");

        if (!card.CanPlayTargeting(target))
            return ActionResult.Fail($"Card [{card.Title}] cannot be played.");

        if (SimulatedPeerRegistry.ShouldHostEnqueueCombatAction(player)) {
            PseudoCoopActionQueue.EnsureQueueForPlayer(player);
            var playAction = new MegaCrit.Sts2.Core.GameActions.PlayCardAction(
                player,
                NetCombatCard.FromModel(card),
                card.Id,
                target?.CombatId);
            RunManager.Instance!.ActionQueueSynchronizer.RequestEnqueue(playAction);
            PseudoCoopActionQueue.MarkInFlight(player.NetId);
            MpAiTeammateHost.NotifyCardQueued(player.NetId, card.Id);
            return ActionResult.Ok($"Queued play [{card.Title}] netId={player.NetId}");
        }

        if (!card.TryManualPlay(target))
            return ActionResult.Fail($"Card [{card.Title}] cannot be played.");

        if (!await Sts2CombatPlayHelper.WaitForManualPlayAsync(card, TimeSpan.FromSeconds(8)))
            return ActionResult.Fail($"Card [{card.Title}] play did not complete.");

        return ActionResult.Ok($"Played [{card.Title}]");
    }

    private static ActionResult EndTurn(Player player)
    {
        var combatState = player.PlayerCombatState;
        if (combatState == null) return ActionResult.Fail("Not in combat.");

        if (!Sts2CombatCompat.IsCombatPlayPhaseActive())
            return ActionResult.Fail("Not in play phase.");

        if (LanAiOwnership.IsHostHandPlayLocal(player))
            return ActionResult.Fail("LAN host local combat is hand-play only.");

        if (SimulatedPeerRegistry.ShouldHostEnqueueCombatAction(player)) {
            MpAiTeammateCombatActions.SignalEndTurn(player);
            return ActionResult.Ok($"Queued end turn netId={player.NetId}.");
        }

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
                return CombatTargetResolver.ResolveEnemy(combatState, card, targetIndex);
            }
            case TargetType.AnyAlly: {
                var allies = combatState.PlayerCreatures.Where(c => c.IsAlive);
                return allies.FirstOrDefault(card.IsValidTarget) ?? player.Creature;
            }
            case TargetType.AnyPlayer:
            case TargetType.Self:
                // Self-target cards use null creature (matches NCardPlay.TryManualPlay).
                return null;
            default:
                return null;
        }
    }

    // ── Map ──

    private async Task<ActionResult> SelectMapNode(RunState state, int nodeIndex)
    {
        var mapScreen = NMapScreen.Instance;
        if (mapScreen == null || !mapScreen.IsOpen)
            return ActionResult.Fail("Map screen not open.");

        var allPoints = UIHelper.FindAll<NMapPoint>((Node)mapScreen);

        List<NMapPoint> available;
        if (state.VisitedMapCoords.Count == 0)
        {
            available = allPoints.Where(mp => mp.Point.coord.row == 0).ToList();
        }
        else
        {
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

        await UIHelper.Click(target);
        return ActionResult.Ok($"Selected map node at ({target.Point.coord.row}, {target.Point.coord.col})");
    }

    private async Task<ActionResult> Proceed()
    {
        if (NOverlayStack.Instance?.Peek() is Node overlayNode)
        {
            var btn = UIHelper.FindFirst<NProceedButton>(overlayNode);
            if (btn is { IsEnabled: true })
            {
                await UIHelper.Click(btn);
                return ActionResult.Ok("Proceeded from overlay.");
            }
        }

        var root = ((SceneTree)Engine.GetMainLoop()).Root;

        var restRoom = root.GetNodeOrNull<NRestSiteRoom>(
            "/root/Game/RootSceneContainer/Run/RoomContainer/RestSiteRoom");
        if (restRoom?.ProceedButton is { IsEnabled: true } restProceed)
        {
            await UIHelper.Click(restProceed);
            return ActionResult.Ok("Left rest site.");
        }

        // Post-combat only: wait for rewards overlay — not for shop/rest/event rooms.
        var cm = CombatManager.Instance;
        var isPostCombatRoom = _stateProvider.TryGetRunAndPlayer(out var state, out _)
            && state.CurrentRoom?.RoomType is RoomType.Monster or RoomType.Elite or RoomType.Boss;
        if (isPostCombatRoom && cm != null && !cm.IsInProgress)
        {
            bool appeared = await UIHelper.WaitUntil(() =>
                NOverlayStack.Instance?.Peek() is NRewardsScreen
                || (NMapScreen.Instance?.IsOpen ?? false),
                TimeSpan.FromSeconds(10));

            return appeared
                ? ActionResult.Ok("Rewards screen or map appeared after combat.")
                : ActionResult.Fail("Timed out waiting for post-combat screen.");
        }

        var roomContainer = root.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer");
        if (roomContainer != null)
        {
            var btn = UIHelper.FindFirst<NProceedButton>(roomContainer);
            if (btn is { IsEnabled: true })
            {
                await UIHelper.Click(btn);
                return ActionResult.Ok("Proceeded from room.");
            }
        }

        return ActionResult.Fail("No proceed button found.");
    }

    // ──────── Rewards ────────

    private async Task<ActionResult> PickCardReward(int cardIndex)
    {
        var screen = OverlayPhaseHelper.FindCardRewardScreen();
        if (screen == null)
            return ActionResult.Fail("Card reward screen not open.");

        if (screen is NChooseACardSelectionScreen chooseScreen) {
            var holders = UIHelper.FindAll<NCardHolder>((Node)chooseScreen)
                .Where(h => GodotObject.IsInstanceValid(h) && h.Visible && h.CardModel != null)
                .ToList();
            if (holders.Count == 0) return ActionResult.Fail("No choose-a-card options found.");

            var idx = cardIndex >= 0 && cardIndex < holders.Count ? cardIndex : 0;
            // Screen ignores presses for 350ms after open.
            await Task.Delay(400);
            holders[idx].EmitSignal(NCardHolder.SignalName.Pressed, holders[idx]);
            return ActionResult.Ok($"Picked choose-a-card option {idx}.");
        }

        if (screen is NCardRewardSelectionScreen cardRewardScreen) {
            var holders = UIHelper.FindAll<NCardHolder>((Node)cardRewardScreen);
            if (holders.Count == 0) return ActionResult.Fail("No card rewards found.");

            var idx = cardIndex >= 0 && cardIndex < holders.Count ? cardIndex : 0;
            holders[idx].EmitSignal(NCardHolder.SignalName.Pressed, holders[idx]);
            if (!await UIHelper.WaitUntil(
                () => !OverlayPhaseHelper.HasActiveCardRewardScreen(),
                TimeSpan.FromSeconds(10)))
                return ActionResult.Fail("Card reward screen did not close after pick.");

            _pendingCardRewardButton = null;
            return ActionResult.Ok($"Picked card reward {idx}.");
        }

        if (screen is NDeckCardSelectScreen deckScreen) {
            var holders = UIHelper.FindAll<NCardHolder>(deckScreen)
                .Where(h => GodotObject.IsInstanceValid(h) && h.Visible)
                .ToList();
            if (holders.Count == 0) return ActionResult.Fail("No deck card choices found.");

            var idx = cardIndex >= 0 && cardIndex < holders.Count ? cardIndex : 0;
            var holder = holders[idx];
            holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);

            var confirm = UIHelper.FindFirst<NProceedButton>(deckScreen);
            if (confirm is { IsEnabled: true }) {
                await UIHelper.Click(confirm);
                return ActionResult.Ok($"Confirmed deck card choice {idx}.");
            }

            return ActionResult.Ok($"Selected deck card {idx} (awaiting confirm).");
        }

        return ActionResult.Fail($"Unsupported card reward screen: {screen.GetType().Name}");
    }

    private async Task<ActionResult> SkipCardReward()
    {
        var screen = OverlayPhaseHelper.FindCardRewardScreen();
        if (screen == null)
            return ActionResult.Fail("Card reward screen not open.");

        if (screen is NChooseACardSelectionScreen chooseScreen) {
            var skip = UIHelper.FindFirst<NChoiceSelectionSkipButton>(chooseScreen);
            if (skip is { Visible: true, IsEnabled: true }) {
                await UIHelper.Click(skip);
                return ActionResult.Ok("Skipped choose-a-card screen.");
            }
        }

        if (screen is NDeckCardSelectScreen deckScreen) {
            var deckBack = UIHelper.FindFirst<NBackButton>(deckScreen);
            if (deckBack != null) {
                await UIHelper.Click(deckBack);
                return ActionResult.Ok("Cancelled deck card selection.");
            }
        }

        if (screen is not NCardRewardSelectionScreen cardRewardScreen)
            return ActionResult.Fail("Card reward screen not open.");

        // Skip is NCardRewardAlternativeButton (OPTION_SKIP), not Back/Proceed — see CardRewardAlternative.Generate.
        // Official skip re-enables the NRewardButton; mark it declined so CollectReward does not re-open the picker.
        await Sts2WaitHelper.ActionsSettled(TimeSpan.FromSeconds(5));

        NCardRewardAlternativeButton? skipBtn = null;
        if (!await UIHelper.WaitUntil(() => {
            skipBtn = UIHelper.FindAll<NCardRewardAlternativeButton>((Node)cardRewardScreen)
                .FirstOrDefault(b => GodotObject.IsInstanceValid(b) && b.Visible && b.IsEnabled);
            return skipBtn != null;
        }, TimeSpan.FromSeconds(2)))
            return ActionResult.Fail("No skip button found on card reward screen.");

        var altCount = UIHelper.FindAll<NCardRewardAlternativeButton>((Node)cardRewardScreen).Count;
        _log($"SkipCardReward: clicking skip (alts={altCount} pendingCard={_pendingCardRewardButton != null})");

        await UIHelper.Click(skipBtn!);

        if (!await UIHelper.WaitUntil(
            () => !OverlayPhaseHelper.HasActiveCardRewardScreen(),
            TimeSpan.FromSeconds(10))) {
            _log("SkipCardReward: screen still open after skip click.");
            return ActionResult.Fail("Card reward screen did not close after skip.");
        }

        await Sts2WaitHelper.ActionsSettled(TimeSpan.FromSeconds(5));

        if (_pendingCardRewardButton != null && GodotObject.IsInstanceValid(_pendingCardRewardButton)) {
            _declinedRewardButtons.Add(_pendingCardRewardButton);
            _attemptedRewardButtons.Add(_pendingCardRewardButton);
            _log("SkipCardReward: card reward marked declined.");
        }

        _cardRewardDeclined = true;
        _pendingCardRewardButton = null;
        return ActionResult.Ok("Skipped card reward.");
    }

    private async Task<ActionResult> CollectReward(int rewardIndex)
    {
        if (NOverlayStack.Instance?.Peek() is not NRewardsScreen screen)
        {
            ResetRewardTracking();
            return ActionResult.Fail("Rewards screen not open.");
        }

        if (_lastRewardScreen != screen)
        {
            ResetRewardTracking();
            _lastRewardScreen = screen;
        }

        // Mirrors official RewardsScreenHandler: drain buttons, wait on UI state (not fixed delays).
        var snapshot = _stateProvider.TakeSnapshot();
        bool hasPotionSlots = _stateProvider.TryGetRunAndPlayer(out _, out var player)
                              && (player?.HasOpenPotionSlots ?? false);

        if (OverlayPhaseHelper.RewardsReadyForMap(screen, player, snapshot)) {
            ResetRewardTracking();
            return ActionResult.Ok("Rewards complete; map is open.");
        }

        var clicked = 0;
        while (clicked < 12) {
            snapshot = _stateProvider.TakeSnapshot();
            hasPotionSlots = player?.HasOpenPotionSlots ?? false;

            var btn = UIHelper.FindAll<NRewardButton>((Node)screen)
                .FirstOrDefault(b => b.IsEnabled
                    && !_attemptedRewardButtons.Contains(b)
                    && !_declinedRewardButtons.Contains(b)
                    && !(_cardRewardDeclined && b.Reward is CardReward));

            if (btn == null)
                break;

            var discardSlot = -1;
            if (btn.Reward is PotionReward potionReward) {
                if (!hasPotionSlots) {
                    if (player == null)
                        return ActionResult.Fail("No player for potion reward.");

                    var incomingId = potionReward.Potion?.Id.Entry;
                    if (!PotionInventoryScorer.ShouldMakeRoom(incomingId, snapshot, out discardSlot)) {
                        _attemptedRewardButtons.Add(btn);
                        _log($"CollectReward: skip potion [{incomingId}] (not worth replacing belt slot).");
                        continue;
                    }

                    var discardResult = await DiscardPotion(player!, discardSlot);
                    if (!discardResult.Success)
                        return discardResult;

                    snapshot = _stateProvider.TakeSnapshot();
                    hasPotionSlots = player!.HasOpenPotionSlots;
                }
            }

            _attemptedRewardButtons.Add(btn);
            if (btn.Reward is CardReward)
                _pendingCardRewardButton = btn;

            var rewardKind = btn.Reward?.GetType().Name ?? "?";
            _log($"CollectReward: clicking [{rewardKind}] (attempted={_attemptedRewardButtons.Count})");
            await UIHelper.Click(btn);
            clicked++;

            if (!await RewardScreenHelper.WaitForClaimAsync(screen, btn, TimeSpan.FromSeconds(10))) {
                if (btn.Reward is PotionReward && player != null) {
                    snapshot = _stateProvider.TakeSnapshot();
                    if (!player.HasOpenPotionSlots) {
                        var incomingId = (btn.Reward as PotionReward)?.Potion?.Id.Entry;
                        if (discardSlot < 0
                            && PotionInventoryScorer.ShouldMakeRoom(incomingId, snapshot, out discardSlot)) {
                            var discardResult = await DiscardPotion(player, discardSlot);
                            if (!discardResult.Success)
                                return discardResult;
                            snapshot = _stateProvider.TakeSnapshot();
                        }

                        if (await PotionRewardHelper.TryResolveFullBeltPrompt(player, snapshot, discardSlot)
                            || player.HasOpenPotionSlots) {
                            _attemptedRewardButtons.Remove(btn);
                            hasPotionSlots = player.HasOpenPotionSlots;
                            continue;
                        }
                    }
                }

                return ActionResult.Fail("Timed out waiting for reward claim.");
            }

            if (btn.Reward is PotionReward && player != null) {
                snapshot = _stateProvider.TakeSnapshot();
                hasPotionSlots = player.HasOpenPotionSlots;
            }

            var top = NOverlayStack.Instance?.Peek();
            if (top != null && top != (IOverlayScreen)screen)
                return ActionResult.Ok("Child overlay opened.");
        }

        if (clicked > 0)
            return ActionResult.Ok($"Collected {clicked} reward(s).");

        // No more buttons — click Proceed (official RewardsScreenHandler also accepts map open).
        if (OverlayPhaseHelper.RewardsReadyForMap(screen, player, snapshot)) {
            ResetRewardTracking();
            return ActionResult.Ok("Rewards complete; map is open.");
        }

        var proceedBtn = UIHelper.FindFirst<NProceedButton>((Node)screen);
        if (proceedBtn != null)
        {
            _log($"CollectReward: all rewards collected — clicking proceed (cardDeclined={_cardRewardDeclined}).");
            await UIHelper.WaitUntil(
                () => proceedBtn.IsEnabled || screen.IsComplete,
                TimeSpan.FromSeconds(10));
            await UIHelper.Click(proceedBtn);
            var settled = await UIHelper.WaitUntil(
                () => !GodotObject.IsInstanceValid((Node)screen)
                      || NOverlayStack.Instance?.Peek() != (IOverlayScreen)screen
                      || (NMapScreen.Instance?.IsOpen ?? false),
                TimeSpan.FromSeconds(15));
            if (settled && OverlayPhaseHelper.RewardsReadyForMap(screen, player, snapshot)) {
                ResetRewardTracking();
                return ActionResult.Ok("Proceed clicked.");
            }

            _log("CollectReward: proceed clicked, awaiting map transition.");
            return ActionResult.Ok("Proceed clicked; awaiting map.");
        }

        return ActionResult.Fail("Rewards screen has no clickable buttons yet.");
    }

    private void ResetRewardTracking()
    {
        _lastRewardScreen = null;
        _attemptedRewardButtons.Clear();
        _declinedRewardButtons.Clear();
        _pendingCardRewardButton = null;
        _cardRewardDeclined = false;
    }

    /// <summary>
    /// Mirrors official AutoSlay TreasureRoomHandler:
    /// open chest → pick up relics → click proceed.
    /// </summary>
    private async Task<ActionResult> HandleTreasureRoom()
    {
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        var room = root.GetNodeOrNull<NTreasureRoom>(
            "/root/Game/RootSceneContainer/Run/RoomContainer/TreasureRoom");
        if (room == null)
            return ActionResult.Fail("TreasureRoom node not found.");

        // 1. Open the chest.
        var chest = room.GetNodeOrNull<NClickableControl>("Chest");
        if (chest != null && chest.IsEnabled)
        {
            _log("TreasureRoom: opening chest.");
            await UIHelper.Click(chest);
            await UIHelper.WaitUntil(
                () => UIHelper.FindAll<NTreasureRoomRelicHolder>((Node)room)
                    .Any(h => h.Visible && h.IsEnabled),
                TimeSpan.FromSeconds(5));
        }

        var relicHolders = UIHelper.FindAll<NTreasureRoomRelicHolder>((Node)room);
        foreach (var holder in relicHolders)
        {
            if (holder.IsEnabled && holder.Visible)
            {
                _log("TreasureRoom: picking up relic.");
                await UIHelper.Click(holder);
                await Sts2WaitHelper.ActionsSettled(TimeSpan.FromSeconds(5));
            }
        }

        var proceedBtn = room.ProceedButton;
        if (proceedBtn != null)
        {
            await UIHelper.WaitUntil(() => proceedBtn.IsEnabled, TimeSpan.FromSeconds(5));
            if (proceedBtn.IsEnabled)
            {
                _log("TreasureRoom: clicking proceed.");
                await UIHelper.Click(proceedBtn);
                return ActionResult.Ok("Treasure room completed.");
            }
        }

        return ActionResult.Ok("TreasureRoom: waiting.");
    }

    private async Task<ActionResult> DismissRewards()
    {
        if (NOverlayStack.Instance?.Peek() is not NRewardsScreen screen)
            return ActionResult.Fail("Rewards screen not open.");

        if (NMapScreen.Instance is { IsOpen: true } && screen.IsComplete)
            return ActionResult.Ok("Rewards complete; map is open.");

        var proceedBtn = UIHelper.FindFirst<NProceedButton>((Node)screen);
        if (proceedBtn == null)
            return ActionResult.Fail("No proceed button on rewards.");

        _log("DismissRewards: clicking proceed.");
        await UIHelper.WaitUntil(
            () => proceedBtn.IsEnabled || screen.IsComplete,
            TimeSpan.FromSeconds(10));
        await UIHelper.Click(proceedBtn);
        await UIHelper.WaitUntil(
            () => !GodotObject.IsInstanceValid((Node)screen)
                  || NOverlayStack.Instance?.Peek() != (IOverlayScreen)screen
                  || (NMapScreen.Instance?.IsOpen ?? false),
            TimeSpan.FromSeconds(10));
        return ActionResult.Ok("Dismissed rewards.");
    }

    private async Task<ActionResult> PickRelic(int relicIndex)
    {
        var screen = OverlayPhaseHelper.FindRelicSelectionScreen();
        if (screen == null)
            return ActionResult.Fail("Relic selection screen not open.");

        var entries = UIHelper.FindAll<NRelicCollectionEntry>((Node)screen)
            .Where(e => e.Visible).ToList();
        if (entries.Count > 0) {
            var idx = relicIndex >= 0 && relicIndex < entries.Count ? relicIndex : 0;
            var entry = entries[idx];
            if (entry is NClickableControl clickable)
                await UIHelper.Click(clickable);
            else
                return ActionResult.Fail("Relic entry is not clickable.");
            return ActionResult.Ok($"Picked relic option {idx}.");
        }

        var holders = UIHelper.FindAll<NTreasureRoomRelicHolder>((Node)screen)
            .Where(h => h.IsEnabled && h.Visible).ToList();
        if (holders.Count > 0) {
            var idx = relicIndex >= 0 && relicIndex < holders.Count ? relicIndex : 0;
            await UIHelper.Click(holders[idx]);
            return ActionResult.Ok($"Picked relic holder {idx}.");
        }

        return ActionResult.Fail("No relic choices found.");
    }

    private async Task<ActionResult> AdvanceOverlay()
    {
        if (OverlayPhaseHelper.FindCardRewardScreen() != null)
            return await PickCardReward(0);

        var proceed = await Proceed();
        if (proceed.Success) return proceed;

        if (NOverlayStack.Instance?.Peek() is Node overlay)
        {
            var back = UIHelper.FindFirst<NBackButton>(overlay);
            if (back is { IsEnabled: true })
            {
                await UIHelper.Click(back);
                return ActionResult.Ok("Dismissed overlay via back.");
            }
        }

        return ActionResult.Fail("Could not advance overlay.");
    }

    // ──────── Events ────────

    private async Task<ActionResult> SelectEventChoice(int choiceIndex)
    {
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        var eventRoom = root.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer/EventRoom");
        if (eventRoom == null) return ActionResult.Fail("Event room not found.");

        var options = UIHelper.FindAll<NEventOptionButton>(eventRoom)
            .Where(o => !o.Option.IsLocked).ToList();
        if (options.Count == 0) return ActionResult.Fail("No event options available.");

        var idx = choiceIndex >= 0 && choiceIndex < options.Count ? choiceIndex : 0;
        await UIHelper.Click(options[idx]);
        return ActionResult.Ok($"Selected event option {idx}.");
    }

    // ──────── Shop ────────

    private async Task<ActionResult> PurchaseShopItem(int itemIndex)
    {
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

    private async Task<ActionResult> RemoveCardAtShop()
    {
        var room = FindMerchantRoom();
        if (room == null) return ActionResult.Fail("Shop not found.");

        var removalSlot = room.Inventory?.GetAllSlots()
            .OfType<NMerchantCardRemoval>()
            .FirstOrDefault(s => s.Entry.IsStocked && s.Entry.EnoughGold);

        if (removalSlot == null) return ActionResult.Fail("Card removal not available or too expensive.");

        await removalSlot.Entry.OnTryPurchaseWrapper(room.Inventory.Inventory);
        return ActionResult.Ok("Initiated card removal.");
    }

    private async Task<ActionResult> LeaveShop()
    {
        var room = FindMerchantRoom();
        if (room == null) return ActionResult.Fail("Shop not found.");

        var backBtn = UIHelper.FindFirst<NBackButton>((Node)room);
        if (backBtn != null)
            await UIHelper.Click(backBtn);

        await UIHelper.WaitUntil(
            () => room.ProceedButton is { IsEnabled: true },
            TimeSpan.FromSeconds(5));

        if (room.ProceedButton is { IsEnabled: true })
            await UIHelper.Click(room.ProceedButton);

        return ActionResult.Ok("Left shop.");
    }

    // ──────── Rest Site ────────

    private async Task<ActionResult> SelectRestSiteOption(int optionIndex)
    {
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

        await UIHelper.Click(button);

        // Heal can open reward overlays (e.g. TinyMailbox potions); wait before the next poll.
        await UIHelper.WaitUntil(() =>
                NOverlayStack.Instance?.Peek() is NRewardsScreen
                || !option.IsEnabled
                || room.ProceedButton is { IsEnabled: true },
            TimeSpan.FromSeconds(8));

        return ActionResult.Ok($"Selected rest option: {option.GetType().Name}");
    }

    // ──────── Potions ────────

    private static async Task<ActionResult> UsePotionAsync(Player player, int potionSlot, int targetIndex)
    {
        var potion = player.GetPotionAtSlotIndex(potionSlot);
        if (potion == null)
            return ActionResult.Fail($"No potion in slot {potionSlot}.");

        var potionId = potion.Id.Entry ?? "";
        Creature? target = null;
        if (potion.TargetType.IsSingleTarget())
        {
            var combatState = player.Creature.CombatState;
            if (combatState != null)
            {
                target = potion.TargetType == TargetType.AnyEnemy
                    ? CombatTargetResolver.ResolveHittableEnemy(
                        combatState, targetIndex >= 0 ? targetIndex : 0)
                    : combatState.PlayerCreatures.FirstOrDefault(c => c.IsAlive);
            }
        }

        potion.EnqueueManualUse(target);

        if (!await Sts2PotionUseHelper.WaitForManualUseAsync(
                player, potionSlot, potionId, TimeSpan.FromSeconds(8)))
            return ActionResult.Fail($"Potion [{potionId}] use did not complete.");

        return ActionResult.Ok($"Used potion [{potionId}] slot {potionSlot}.");
    }

    private static async Task<ActionResult> DiscardPotion(Player player, int potionSlot)
    {
        var potion = player.GetPotionAtSlotIndex(potionSlot);
        if (potion == null)
            return ActionResult.Fail($"No potion in slot {potionSlot}.");

        await PotionActions.DiscardPotion(potion);
        return ActionResult.Ok($"Discarded potion [{potion.Id.Entry}] from slot {potionSlot}.");
    }

    // ──────── Helpers ────────

    private static NMerchantRoom? FindMerchantRoom()
    {
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        return root.GetNodeOrNull<NMerchantRoom>(
            "/root/Game/RootSceneContainer/Run/RoomContainer/MerchantRoom");
    }
}
