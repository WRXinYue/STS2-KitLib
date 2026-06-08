using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Presets;

/// <summary>
/// Manages all preset types: loadout, card, relic, power, potion, event, encounter, monster.
/// Provides capture-from-run and apply-to-run operations.
/// </summary>
internal static class PresetManager {
    private static string PresetsDir => DataPaths.PresetsDir;

    private static readonly Lazy<PresetStore<LoadoutPreset>> _loadouts = new(() => {
        var store = new PresetStore<LoadoutPreset>(Path.Combine(PresetsDir, "loadouts.json"));
        store.Load();
        return store;
    });

    public static PresetStore<LoadoutPreset> Loadouts => _loadouts.Value;

    /// <summary>Capture current run state into a LoadoutPreset, limited to <paramref name="scope"/>.</summary>
    /// <param name="includeCombatSnapshot">When true and in combat, also snapshot hand/draw/discard piles.</param>
    public static LoadoutPreset? CaptureFromRun(PresetContents scope = PresetContents.All, bool includeCombatSnapshot = false) {
        if (!RunContext.TryGetRunAndPlayer(out _, out var player)) return null;

        var preset = new LoadoutPreset { Contents = scope };

        if (scope.HasFlag(PresetContents.Stats)) {
            preset.Gold = player.Gold;
            preset.CurrentHp = player.Creature.CurrentHp;
            preset.MaxHp = player.Creature.MaxHp;
            preset.Energy = player.PlayerCombatState?.Energy ?? 0;
            preset.MaxEnergy = player.MaxEnergy;
            preset.Stars = player.PlayerCombatState?.Stars ?? 0;
            preset.OrbSlots = player.BaseOrbSlotCount;
        }

        if (scope.HasFlag(PresetContents.Cards)) {
            var deckCards = player.Deck?.Cards;
            if (deckCards != null) {
                preset.Cards.AddRange(GroupCards(deckCards));
            }

            if (includeCombatSnapshot && CombatManager.Instance?.IsInProgress == true) {
                preset.HandCards = SnapshotPile(player, PileType.Hand);
                preset.DrawCards = SnapshotPile(player, PileType.Draw);
                preset.DiscardCards = SnapshotPile(player, PileType.Discard);
            }
        }

        if (scope.HasFlag(PresetContents.Relics)) {
            var relics = player.Relics;
            if (relics != null) {
                foreach (var relic in relics) {
                    if (relic != null)
                        preset.Relics.Add(((AbstractModel)relic).Id.Entry);
                }
            }
        }

        return preset;
    }

    /// <summary>
    /// Apply a LoadoutPreset to the current run.
    /// <paramref name="scope"/> limits which parts are applied; intersected with <c>preset.Contents</c>.
    /// </summary>
    public static async Task ApplyToRunAsync(LoadoutPreset preset, PresetContents scope = PresetContents.All) {
        if (!RunContext.TryGetRunAndPlayer(out var runState, out var player)) return;

        var effective = preset.Contents & scope;
        var inCombat = MegaCrit.Sts2.Core.Combat.CombatManager.Instance?.IsInProgress == true;
        int applied = 0;

        if (effective.HasFlag(PresetContents.Stats)) {
            try {
                await PlayerCmd.SetGold((decimal)preset.Gold, player);
                await Sts2ApiCompat.SetMaxHpAsync(player.Creature, preset.MaxHp);
                await Sts2ApiCompat.SetCurrentHpAsync(player.Creature, preset.CurrentHp);
                player.MaxEnergy = preset.MaxEnergy;
                player.BaseOrbSlotCount = preset.OrbSlots;
                applied++;
                MainFile.Logger.Info("[KitLib] Stats applied.");
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[KitLib] Stats apply failed: {ex.Message}");
            }
        }

        if (effective.HasFlag(PresetContents.Relics)) {
            try {
                foreach (var relic in player.Relics.ToArray())
                    if (relic != null) await RelicCmd.Remove(relic);

                foreach (var relicId in preset.Relics) {
                    var model = ModelDb.AllRelics.FirstOrDefault(r => ((AbstractModel)r).Id.Entry == relicId);
                    if (model != null)
                        await RelicCmd.Obtain(model.ToMutable(), player, -1);
                }
                applied++;
                MainFile.Logger.Info("[KitLib] Relics applied.");
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[KitLib] Relics apply failed: {ex.Message}");
            }
        }

        if (effective.HasFlag(PresetContents.Cards)) {
            try {
#if STS2_BETA
                var combatState = player.Creature?.CombatState as MegaCrit.Sts2.Core.Combat.CombatState;
#else
                var combatState = player.Creature?.CombatState;
#endif

                if (inCombat && combatState != null) {
                    var combatCards = new List<CardModel>();
                    foreach (PileType pt in new[] { PileType.Hand, PileType.Draw, PileType.Discard }) {
                        var pile = pt.GetPile(player);
                        if (pile != null)
                            combatCards.AddRange(pile.Cards.ToArray());
                    }
                    MainFile.Logger.Info($"[KitLib] Removing {combatCards.Count} combat cards...");
                    if (combatCards.Count > 0)
                        await CardPileCmd.RemoveFromCombat(combatCards, false);
                }

                var deckCards = player.Deck.Cards.ToArray();
                MainFile.Logger.Info($"[KitLib] Removing {deckCards.Length} deck cards...");
                foreach (var card in deckCards)
                    if (card != null) await CardPileCmd.RemoveFromDeck(card, false);

                foreach (var entry in preset.Cards) {
                    var model = ModelDb.AllCards.FirstOrDefault(c => ((AbstractModel)c).Id.Entry == entry.CardId);
                    if (model == null) continue;
                    for (int i = 0; i < entry.Count; i++) {
                        var deckCard = ((RunState)runState).CreateCard(model.CanonicalInstance, player);
                        for (int u = 0; u < entry.UpgradeLevel; u++)
                            CardCmd.Upgrade(deckCard);
                        await CardPileCmd.Add(deckCard, PileType.Deck, skipVisuals: true);
                    }
                }

                if (inCombat && combatState != null) {
                    if (preset.HasCombatSnapshot) {
                        MainFile.Logger.Info("[KitLib] Restoring combat snapshot (hand/draw/discard)...");
                        await AddCombatCardsFromEntries(preset.HandCards, PileType.Hand, combatState, player);
                        await AddCombatCardsFromEntries(preset.DrawCards, PileType.Draw, combatState, player);
                        await AddCombatCardsFromEntries(preset.DiscardCards, PileType.Discard, combatState, player);
                    }
                    else {
                        foreach (var entry in preset.Cards) {
                            var model = ModelDb.AllCards.FirstOrDefault(c => ((AbstractModel)c).Id.Entry == entry.CardId);
                            if (model == null) continue;
                            for (int i = 0; i < entry.Count; i++) {
                                var combatCard = combatState.CreateCard(model.CanonicalInstance, player);
                                for (int u = 0; u < entry.UpgradeLevel; u++)
                                    CardCmd.Upgrade(combatCard);
#if STS2_BETA
                                await CardPileCmd.AddGeneratedCardToCombat(combatCard, PileType.Draw, player);
#else
                                await CardPileCmd.AddGeneratedCardToCombat(combatCard, PileType.Draw, addedByPlayer: true);
#endif
                                combatCard.Pile?.InvokeCardAddFinished();
                            }
                        }
                        const int handDraw = 5;
                        MainFile.Logger.Info($"[KitLib] No snapshot — drawing {handDraw} cards into hand...");
                        await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), handDraw, player, false);
                    }
                }
                applied++;
                MainFile.Logger.Info("[KitLib] Cards applied.");
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[KitLib] Cards apply failed: {ex.Message}");
            }
        }

        if (applied > 0)
            MainFile.Logger.Info($"[KitLib] Preset applied (scope: {effective}, inCombat: {inCombat}).");
        else
            MainFile.Logger.Warn("[KitLib] Preset apply: nothing was applied.");
    }

    /// <summary>Export a preset to clipboard as JSON.</summary>
    public static string ExportToClipboard(string name, LoadoutPreset preset) {
        var payload = new { name, preset };
        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        Godot.DisplayServer.ClipboardSet(json);
        return json;
    }

    /// <summary>Import a preset from clipboard JSON.</summary>
    public static (string? name, LoadoutPreset? preset) ImportFromClipboard() {
        try {
            var json = Godot.DisplayServer.ClipboardGet();
            if (string.IsNullOrWhiteSpace(json)) return (null, null);

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            var presetJson = root.TryGetProperty("preset", out var p) ? p.GetRawText() : null;
            if (presetJson == null) return (null, null);

            var preset = System.Text.Json.JsonSerializer.Deserialize<LoadoutPreset>(presetJson);
            return (name, preset);
        }
        catch {
            return (null, null);
        }
    }

    // ─────────────────────────────── Helpers ───────────────────────────────

    private static List<LoadoutCardEntry> GroupCards(IEnumerable<CardModel> cards) {
        return cards
            .Where(c => c != null)
            .GroupBy(c => new { Id = ((AbstractModel)c).Id.Entry, Upgrade = c.CurrentUpgradeLevel })
            .Select(g => new LoadoutCardEntry {
                CardId = g.Key.Id,
                Count = g.Count(),
                UpgradeLevel = g.Key.Upgrade,
            })
            .ToList();
    }

    private static List<LoadoutCardEntry>? SnapshotPile(Player player, PileType pileType) {
        var pile = pileType.GetPile(player);
        if (pile == null || pile.Cards.Count == 0) return new List<LoadoutCardEntry>();
        return GroupCards(pile.Cards);
    }

    private static async Task AddCombatCardsFromEntries(
        List<LoadoutCardEntry>? entries, PileType pileType,
        CombatState combatState, Player player) {
        if (entries == null || entries.Count == 0) return;
        foreach (var entry in entries) {
            var model = ModelDb.AllCards.FirstOrDefault(c => ((AbstractModel)c).Id.Entry == entry.CardId);
            if (model == null) {
                MainFile.Logger.Warn($"[KitLib] Card not found for {pileType}: {entry.CardId}");
                continue;
            }
            for (int i = 0; i < entry.Count; i++) {
                var combatCard = combatState.CreateCard(model.CanonicalInstance, player);
                for (int u = 0; u < entry.UpgradeLevel; u++)
                    CardCmd.Upgrade(combatCard);
#if STS2_BETA
                await CardPileCmd.AddGeneratedCardToCombat(combatCard, pileType, player);
#else
                await CardPileCmd.AddGeneratedCardToCombat(combatCard, pileType, addedByPlayer: true);
#endif
                if (pileType is PileType.Draw or PileType.Discard)
                    combatCard.Pile?.InvokeCardAddFinished();
            }
        }
    }
}
