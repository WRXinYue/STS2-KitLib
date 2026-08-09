using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KitLib.Models;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions;

/// <summary>One entry in the card test queue: which card and how many upgrade levels to apply.</summary>
internal sealed class CardTestEntry {
    public CardModel Card { get; }
    public int UpgradeLevels { get; set; }

    public CardTestEntry(CardModel card, int upgradeLevels = 0) {
        Card = card;
        UpgradeLevels = upgradeLevels;
    }
}

internal static class CardTestActions {
    internal static bool IsAtRestSite(RunState state) =>
        state.CurrentRoom?.RoomType == RoomType.RestSite;

    internal static bool CanRunCardTest(RunState state, Player player) {
        if (MpCheatSession.InMultiplayerRun)
            return false;
        return player.PlayerCombatState != null || IsAtRestSite(state);
    }

    /// <summary>Teleports the run into a KitLib-owned card test combat (<see cref="KitLibCardTestEncounter"/>).</summary>
    internal static bool TryEnterTestRoom() {
        try {
            var rm = RunManager.Instance;
            if (rm == null || !rm.IsInProgress) {
                MainFile.Logger.Warn("CardTestActions: No run in progress for test room.");
                return false;
            }
            if (MpCheatSession.InMultiplayerRun) {
                MainFile.Logger.Warn("CardTestActions: Test room not available in multiplayer.");
                return false;
            }
            var encounter = ModelDb.Encounter<KitLibCardTestEncounter>().ToMutable();
            TaskHelper.RunSafely(rm.EnterRoomDebug(RoomType.Monster, MapPointType.Monster, encounter));
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"CardTestActions.TryEnterTestRoom failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Runs the full test queue: clears hand, tests each entry, auto-resolves blocking choices.
    /// </summary>
    internal static async Task TestQueue(
        IReadOnlyList<CardTestEntry> queue,
        CardTarget target,
        RunState state,
        Player player) {
        if (queue.Count == 0)
            return;

        CardTestState.TestingActive = true;
        try {
            if (IsAtRestSite(state)) {
                for (var i = 0; i < queue.Count; i++)
                    await TestEntryAtRestSite(queue[i], i, queue.Count, state, player);
                MainFile.Logger.Info($"CardTestActions: Finished smith-previewing {queue.Count} card(s).");
            }
            else {
                for (var i = 0; i < queue.Count; i++)
                    await TestEntry(queue[i], i, queue.Count, target, state, player);
                MainFile.Logger.Info($"CardTestActions: Finished testing {queue.Count} card(s).");
            }
        }
        finally {
            CardTestState.TestingActive = false;
        }
    }

    /// <summary>
    /// Rest-site pass: smith-style card preview for base and upgraded copies (no combat play).
    /// </summary>
    static async Task TestEntryAtRestSite(
        CardTestEntry template,
        int index,
        int total,
        RunState state,
        Player player) {
        var id = ((AbstractModel)template.Card).Id.Entry;
        MainFile.Logger.Info($"CardTestActions: [{index + 1}/{total}] Smith preview {id}");

        var upgLevels = Math.Max(template.UpgradeLevels, 1);

        await SmithPreviewAsync(state, player, template.Card, 0);
        await SmithPreviewAsync(state, player, template.Card, upgLevels);
    }

    static async Task SmithPreviewAsync(RunState state, Player player, CardModel template, int upgradeLevels) {
        var id = ((AbstractModel)template).Id.Entry;
        var card = state.CreateCard(template.CanonicalInstance, player);
        for (var i = 0; i < upgradeLevels; i++)
            CardCmd.Upgrade(card, CardPreviewStyle.None);

        var container = NRun.Instance?.GlobalUi?.CardPreviewContainer;
        if (container == null) {
            MainFile.Logger.Warn("CardTestActions: No CardPreviewContainer for smith preview.");
            return;
        }

        var vfx = NCardSmithVfx.Create(new[] { card });
        if (vfx == null) {
            MainFile.Logger.Warn($"CardTestActions: Smith preview failed for {id} +{upgradeLevels}.");
            return;
        }

        container.AddChildSafely(vfx);
        MainFile.Logger.Info($"CardTestActions: Smith preview {id} +{upgradeLevels}.");
        await Cmd.CustomScaledWait(1f, 2f, ignoreCombatEnd: true);
    }

    /// <summary>
    /// Clears all combat cards, injects one card, and returns the hand instance if any.
    /// </summary>
    internal static async Task<CardModel?> InjectOne(
        CardTestEntry entry,
        CardTarget target,
        RunState state,
        Player player) {
        if (MpCheatSession.InMultiplayerRun) {
            MainFile.Logger.Warn("CardTestActions: Inject not synced in multiplayer — inject aborted.");
            return null;
        }

        await CardTestPlayHelper.ClearCombatCards(player);
        await CardTestPlayHelper.WaitForCombatSettledAsync();
        await CardTestPlayHelper.SeedDrawPileAsync(state, player);

        var id = ((AbstractModel)entry.Card).Id.Entry;
        var hand = player.PlayerCombatState?.Hand;
        var before = hand?.Cards.ToHashSet() ?? [];

        try {
            await CardActions
                .Add(state, player, entry.Card)
                .Target(target)
                .UpgradeLevels(entry.UpgradeLevels)
                .Duration(EffectDuration.Temporary)
                .RunAsync();
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"CardTestActions: Failed to inject {id}: {ex.Message}");
            return null;
        }

        if (hand == null)
            return null;

        var injected = hand.Cards.FirstOrDefault(c => !before.Contains(c));
        if (injected == null)
            MainFile.Logger.Warn($"CardTestActions: Injected {id} +{entry.UpgradeLevels} but no new hand card found (hand may be full).");
        else
            MainFile.Logger.Info($"CardTestActions: Injected {id} +{entry.UpgradeLevels} into hand.");

        return injected;
    }

    /// <summary>
    /// Tests one queue entry: inject base → play, then inject upgraded → play.
    /// </summary>
    internal static async Task TestEntry(
        CardTestEntry template,
        int index,
        int total,
        CardTarget target,
        RunState state,
        Player player) {
        var id = ((AbstractModel)template.Card).Id.Entry;
        MainFile.Logger.Info($"CardTestActions: [{index + 1}/{total}] Testing {id}");

        var upgLevels = Math.Max(template.UpgradeLevels, 1);

        var baseCard = await InjectOne(new CardTestEntry(template.Card, 0), target, state, player);
        if (baseCard != null)
            await PlayOne(player, baseCard, 0);
        await CardTestPlayHelper.ClearCombatCards(player);

        var upgCard = await InjectOne(new CardTestEntry(template.Card, upgLevels), target, state, player);
        if (upgCard != null)
            await PlayOne(player, upgCard, upgLevels);
        await CardTestPlayHelper.ClearCombatCards(player);
    }

    /// <summary>
    /// Plays a single injected card with auto-resolved targeting.
    /// </summary>
    internal static async Task<bool> PlayOne(Player player, CardModel card, int upgradeLevels) {
        var combatState = player.PlayerCombatState;
        if (combatState == null) {
            MainFile.Logger.Warn("CardTestActions: Not in combat — play aborted.");
            return false;
        }

        if (MpCheatSession.InMultiplayerRun) {
            MainFile.Logger.Warn("CardTestActions: Play not synced in multiplayer — play aborted.");
            return false;
        }

        var id = ((AbstractModel)card).Id.Entry;
        var target = ResolveTarget(player, card);

        CardTestState.ActiveTestCard = card;
        try {
            await CardTestPlayHelper.WaitForCombatSettledAsync();

            if (!card.CanPlayTargeting(target)) {
                MainFile.Logger.Info($"CardTestActions: Skipped {id} +{upgradeLevels} — cannot play.");
                return false;
            }

            if (!card.TryManualPlay(target)) {
                MainFile.Logger.Info($"CardTestActions: TryManualPlay failed for {id} +{upgradeLevels}.");
                return false;
            }

            if (!await CardTestPlayHelper.WaitForPlayAsync(card, TimeSpan.FromSeconds(12))) {
                MainFile.Logger.Info($"CardTestActions: Timed out waiting for {id} +{upgradeLevels} to finish.");
                return false;
            }

            MainFile.Logger.Info($"CardTestActions: Played {id} +{upgradeLevels}.");
            return true;
        }
        finally {
            CardTestState.ActiveTestCard = null;
        }
    }

    /// <summary>
    /// Plays every card currently in the player's hand, one at a time.
    /// Prefer <see cref="PlayOne"/> for card testing — this plays the entire hand snapshot.
    /// </summary>
    internal static async Task PlayAll(Player player) {
        var combatState = player.PlayerCombatState;
        if (combatState == null) {
            MainFile.Logger.Warn("CardTestActions: Not in combat — PlayAll aborted.");
            return;
        }

        if (MpCheatSession.InMultiplayerRun) {
            MainFile.Logger.Warn("CardTestActions: Play not synced in multiplayer — PlayAll aborted.");
            return;
        }

        // Snapshot hand to avoid mutation-while-iterating issues.
        var toPlay = combatState.Hand?.Cards.ToList() ?? new List<CardModel>();
        var played = 0;

        foreach (var card in toPlay) {
            // Re-check combat is still active between plays.
            if (player.PlayerCombatState == null) break;

            var target = ResolveTarget(player, card);

            if (!card.CanPlayTargeting(target)) {
                MainFile.Logger.Info($"CardTestActions: Skipped [{card.Title}] — cannot play.");
                continue;
            }

            if (!card.TryManualPlay(target)) {
                MainFile.Logger.Info($"CardTestActions: TryManualPlay failed for [{card.Title}].");
                continue;
            }

            played++;
            // Yield between plays so STS2 can process the play action and update game state.
            await Task.Delay(150);
        }

        MainFile.Logger.Info($"CardTestActions: Played {played}/{toPlay.Count} card(s).");
    }

    private static Creature? ResolveTarget(Player player, CardModel card) {
        var combatState = player.Creature?.CombatState;
        if (combatState == null) return null;

        return card.TargetType switch {
            TargetType.AnyEnemy => combatState.HittableEnemies.FirstOrDefault(),
            TargetType.AnyAlly => combatState.PlayerCreatures
                .Where(c => c.IsAlive)
                .FirstOrDefault(card.IsValidTarget) ?? player.Creature,
            _ => null,
        };
    }
}
