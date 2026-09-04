using System;
using System.Linq;
using System.Threading.Tasks;
using KitLib.AI.Core;
using KitLib.AI.Sts2.Helpers;
using KitLib.Game;
using KitLib.Host;
using KitLib.Multiplayer.Play;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Singleplayer.Companion;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Sts2;

/// <summary>
/// KitAI play path: companion / LAN / net enqueue, then <see cref="KitLibGameOps"/>.
/// </summary>
public sealed class Sts2ActionExecutor : IGameActionExecutor {
    private readonly Sts2StateProvider _stateProvider;

    public Sts2ActionExecutor(Sts2StateProvider stateProvider, Action<string> log) {
        _stateProvider = stateProvider;
        _ = log;
    }

    static ActionResult? RejectSpCompanionSharedUi(Player? player, string actionName) {
        if (player == null || !SpvCompanionRegistry.IsCompanion(player) || !SpvCompanionRegistry.IsSingleplayerRun())
            return null;

        return ActionResult.Fail($"SP companion cannot drive shared local UI ({actionName}).");
    }

    public async Task<ActionResult> ExecuteAsync(GameAction action) {
        if (!_stateProvider.TryGetRunAndPlayer(out var state, out var player))
            return ActionResult.Fail("No active run or player.");

        if (action.Type is ActionType.PlayCard or ActionType.EndTurn or ActionType.UsePotion) {
            var special = await TryCombatSpecialAsync(player, action);
            if (special != null)
                return special;
        }

        if (action.Type is ActionType.SelectMapNode or ActionType.PickCardReward or ActionType.SkipCardReward
            or ActionType.CollectReward or ActionType.PickRelic) {
            if (RejectSpCompanionSharedUi(player, action.Type.ToString()) is { } rejected)
                return rejected;
        }

        return await KitLibGameOps.ExecuteFor(state, player, action);
    }

    async Task<ActionResult?> TryCombatSpecialAsync(Player player, GameAction action) {
        if (player.PlayerCombatState == null)
            return null;

        if (LanAiOwnership.IsHostHandPlayLocal(player))
            return ActionResult.Fail("LAN host local combat is hand-play only.");

        if (action.Type == ActionType.PlayCard)
            return await TryNetOrCompanionPlayCard(player, action.TargetIndex, action.SecondaryIndex);

        if (action.Type == ActionType.EndTurn)
            return TryNetOrCompanionEndTurn(player);

        return null;
    }

    async Task<ActionResult?> TryNetOrCompanionPlayCard(Player player, int cardIndex, int targetIndex) {
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

        if (NetCombatCommands.TryEnqueuePlayCard(player, card, target)) {
            MpAiTeammateHost.NotifyCardQueued(player.NetId, card.Id);
            return ActionResult.Ok($"Queued play [{card.Title}] netId={player.NetId}");
        }

        if (SpvCompanionRegistry.IsCompanion(player)) {
            using (SpvCompanionCardSelectScope.Enter()) {
                await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), card, target);
            }
            return ActionResult.Ok($"Auto-played [{card.Title}] netId={player.NetId}");
        }

        return null;
    }

    static ActionResult? TryNetOrCompanionEndTurn(Player player) {
        if (!Sts2CombatCompat.IsCombatPlayPhaseActive())
            return ActionResult.Fail("Not in play phase.");

        if (SpvCompanionRegistry.IsCompanion(player) && SpvCompanionRegistry.IsSingleplayerRun()) {
            SpvCompanionCombatActions.SignalEndTurn(player);
            return ActionResult.Ok($"Companion ready to end turn netId={player.NetId}.");
        }

        if (HostDrivenPeers.ShouldHostEnqueueCombatAction(player)) {
            NetCombatCommands.SignalEndTurn(player);
            return ActionResult.Ok($"Queued end turn netId={player.NetId}.");
        }

        return null;
    }

    static Creature? ResolveCardTarget(Player player, CardModel card, int targetIndex) {
        var combatState = player.Creature.CombatState;
        if (combatState == null) return null;

        switch (card.TargetType) {
            case TargetType.AnyEnemy: {
                    if (combatState.HittableEnemies.Any() == false)
                        return null;
                    return KitLib.AI.Sts2.Helpers.CombatTargetResolver.ResolveEnemy((CombatState)combatState, card, targetIndex);
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
}
