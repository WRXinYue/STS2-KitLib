using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using NetCreatureState = MegaCrit.Sts2.Core.Entities.Multiplayer.NetFullCombatState.CreatureState;
using NetPlayerState = MegaCrit.Sts2.Core.Entities.Multiplayer.NetFullCombatState.PlayerState;
using NetCardState = MegaCrit.Sts2.Core.Entities.Multiplayer.NetFullCombatState.CardState;
using NetPileState = MegaCrit.Sts2.Core.Entities.Multiplayer.NetFullCombatState.CombatPileState;

namespace KitLib.Combat;

internal static class CombatCheckpointRestorer {
    private static int _restoreInProgress;

    internal static bool TryRestoreFromFile(string combatPath) {
        if (!CombatSnapshotIO.TryLoad(combatPath, out var snapshot))
            return false;
        if (CombatManager.Instance is not { IsInProgress: true })
            return false;
        if (Interlocked.CompareExchange(ref _restoreInProgress, 1, 0) != 0)
            return false;

        TaskHelper.RunSafely(RestoreAsync(snapshot));
        return true;
    }

    private static async Task RestoreAsync(CombatSnapshot snapshot) {
        var combatManager = CombatManager.Instance;
        var runManager = RunManager.Instance;

        try {
            if (combatManager == null || runManager == null)
                return;

            combatManager.Unpause();
            NCombatRoom.Instance?.Ui.Hand.CancelAllCardPlay();

            await runManager.ActionExecutor.FinishedExecutingActions();
            await YieldFrame();

            if (runManager.DebugOnlyGetState() is not RunState runState)
                return;
            var combatState = combatManager.DebugOnlyGetState();
            if (combatState == null)
                return;

            RestoreCreatures(combatState, snapshot.State.Creatures);
            RestorePlayers(runState, combatState, snapshot.State.Players);

            combatState.RoundNumber = snapshot.Round;
            combatState.CurrentSide = snapshot.CurrentSide;

            RefreshHandUi();

            MainFile.Logger.Info($"CombatCheckpointRestorer: restored combat snapshot (round {snapshot.Round}).");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"CombatCheckpointRestorer: restore failed: {ex.Message}");
        }
        finally {
            combatManager?.Unpause();
            Interlocked.Exchange(ref _restoreInProgress, 0);
        }
    }

    private static void RefreshHandUi() {
        var hand = NCombatRoom.Instance?.Ui.Hand;
        hand?.ForceRefreshCardIndices();
    }

    private static async Task YieldFrame() {
        if (NGame.Instance != null)
            await NGame.Instance.ToSignal(NGame.Instance.GetTree(), Godot.SceneTree.SignalName.ProcessFrame);
    }

    private static void RestoreCreatures(CombatState combatState, List<NetCreatureState> saved) {
        var live = combatState.Creatures.ToList();
        var used = new HashSet<Creature>();

        for (int i = 0; i < saved.Count; i++) {
            var creatureState = saved[i];
            var creature = ResolveCreature(live, used, creatureState, i);
            if (creature == null)
                continue;

            used.Add(creature);
            RestoreCreature(creature, creatureState);
        }
    }

    private static Creature? ResolveCreature(
        List<Creature> live,
        HashSet<Creature> used,
        NetCreatureState saved,
        int index) {
        if (index < live.Count && !used.Contains(live[index]) && CreatureMatches(live[index], saved))
            return live[index];

        return live.FirstOrDefault(c => !used.Contains(c) && CreatureMatches(c, saved));
    }

    private static bool CreatureMatches(Creature creature, NetCreatureState saved) {
        if (saved.playerId.HasValue)
            return creature.Player?.NetId == saved.playerId;
        if (saved.monsterId != null)
            return creature.Monster?.Id == saved.monsterId;
        return false;
    }

    /// <summary>Sync internal restore — avoids CreatureCmd/PowerCmd hooks that deadlock mid-turn.</summary>
    private static void RestoreCreature(Creature creature, NetCreatureState saved) {
        if (creature.MaxHp != (int)saved.maxHp)
            InvokeInternal(creature, "SetMaxHpInternal", saved.maxHp);
        if (creature.CurrentHp != (int)saved.currentHp)
            InvokeInternal(creature, "SetCurrentHpInternal", saved.currentHp);
        RestoreBlock(creature, saved.block);
        RestorePowers(creature, saved.powers);
    }

    private static void InvokeInternal(Creature creature, string methodName, decimal amount) {
        var method = typeof(Creature).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(typeof(Creature).FullName, methodName);

        var paramType = method.GetParameters()[0].ParameterType;
        object value = paramType == typeof(decimal) ? amount
            : paramType == typeof(int) ? (int)Math.Round(amount)
            : Convert.ChangeType(amount, paramType);
        method.Invoke(creature, new[] { value });
    }

    private static void RestoreBlock(Creature creature, int targetBlock) {
        int currentBlock = (int)creature.Block;
        if (targetBlock > currentBlock)
            creature.GainBlockInternal(targetBlock - currentBlock);
        else if (targetBlock < currentBlock)
            creature.LoseBlockInternal(currentBlock - targetBlock);
    }

    private static void RestorePowers(Creature creature, List<NetFullCombatState.PowerState> saved) {
        var unmatched = saved.ToList();

        foreach (var power in creature.Powers.ToArray()) {
            if (power == null)
                continue;

            int index = unmatched.FindIndex(p => p.id == power.Id);
            if (index >= 0) {
                int target = unmatched[index].amount;
                if (power.Amount != target)
                    power.SetAmount(target, silent: true);
                unmatched.RemoveAt(index);
                continue;
            }

            try {
                power.RemoveInternal();
            }
            catch {
                // Mod powers may reject silent removal; continue with remaining state.
            }
        }

        foreach (var powerState in unmatched) {
            try {
                var model = ModelDb.GetByIdOrNull<PowerModel>(powerState.id);
                if (model == null)
                    continue;
                var mutable = model.ToMutable(0);
                mutable.ApplyInternal(creature, powerState.amount, silent: true);
            }
            catch {
                // Skip powers that cannot be re-applied silently (e.g. missing mod content).
            }
        }
    }

    private static void RestorePlayers(RunState runState, CombatState combatState, List<NetPlayerState> saved) {
        foreach (var playerState in saved) {
            var player = runState.Players.FirstOrDefault(p => p.NetId == playerState.playerId);
            if (player?.PlayerCombatState == null)
                continue;

            player.PlayerCombatState.Energy = playerState.energy;
            player.PlayerCombatState.Stars = playerState.stars;
            PreflightCardSnapshots(playerState.piles);
            ClearCombatPilesSilent(player, combatState);
            RestorePilesSilent(runState, combatState, player, playerState.piles);
        }
    }

    /// <summary>Silent pile reset — bypasses CardPileCmd hooks that deadlock under mod combat.</summary>
    private static void ClearCombatPilesSilent(Player player, CombatState combatState) {
        var pcs = player.PlayerCombatState!;
        foreach (var card in pcs.AllCards.ToList()) {
            DestroyCardNode(card);
            if (card.Pile != null)
                card.RemoveFromCurrentPile(silent: true);
            if (combatState.ContainsCard(card))
                combatState.RemoveCard(card);
            card.HasBeenRemovedFromState = true;
        }

        foreach (var pile in pcs.AllPiles)
            pile.InvokeContentsChanged();
    }

    private static void DestroyCardNode(CardModel card) {
        var ui = NCombatRoom.Instance?.Ui;
        if (ui == null)
            return;

        try {
            if (ui.Hand.GetCardHolder(card) != null)
                ui.Hand.Remove(card);
            else
                NCard.FindOnTable(card)?.QueueFreeSafely();
        }
        catch {
            NCard.FindOnTable(card)?.QueueFreeSafely();
        }
    }

    private static void RestorePilesSilent(
        RunState runState,
        CombatState combatState,
        Player player,
        List<NetPileState> piles) {
        foreach (var pileState in piles) {
            var pile = pileState.pileType.GetPile(player);
            if (pile == null)
                continue;

            foreach (var cardState in pileState.cards) {
                var combatCard = CreateCombatCard(runState, combatState, player, cardState);
                pile.AddInternal(combatCard, silent: true);
                if (pileState.pileType == PileType.Hand)
                    AttachHandCardNode(combatCard);
            }

            if (pileState.pileType is PileType.Draw or PileType.Discard or PileType.Exhaust)
                pile.InvokeCardAddFinished();
            else if (pileState.pileType == PileType.Hand)
                pile.InvokeContentsChanged();
        }

        RefreshHandUi();
    }

    private static void AttachHandCardNode(CardModel card) {
        var ui = NCombatRoom.Instance?.Ui;
        if (ui == null)
            return;

        var nCard = NCard.Create(card);
        ui.AddChildSafely(nCard);
        nCard.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
        nCard.Position = PileType.Hand.GetTargetPosition(nCard);
        ui.Hand.Add(nCard);
    }

    private static void PreflightCardSnapshots(List<NetPileState> piles) {
        foreach (var pileState in piles) {
            foreach (var cardState in pileState.cards)
                CardModel.FromSerializable(cardState.card);
        }
    }

    /// <summary>FromSerializable yields mutable instances; use LoadCard + CloneCard, not CreateCard (ToMutable).</summary>
    private static CardModel CreateCombatCard(
        RunState runState,
        CombatState combatState,
        Player player,
        NetCardState cardState) {
        var runCard = runState.LoadCard(cardState.card, player);
        return combatState.CloneCard(runCard);
    }
}
