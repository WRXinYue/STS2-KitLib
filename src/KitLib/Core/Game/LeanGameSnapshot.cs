using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using KitLib.Game;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Game;

internal static class LeanGameSnapshot {
    public static JsonObject Capture(RunState state, Player player, GamePhase phase) {
        var obj = new JsonObject {
            ["phase"] = phase.ToString(),
            ["totalFloor"] = state.TotalFloor,
            ["actIndex"] = state.CurrentActIndex,
            ["actFloor"] = state.ActFloor,
            ["gold"] = player.Gold,
            ["currentHp"] = player.Creature.CurrentHp,
            ["maxHp"] = player.Creature.MaxHp,
            ["characterId"] = player.Character.Id.Entry ?? "",
            ["ascensionLevel"] = state.AscensionLevel,
            ["hasOpenPotionSlots"] = player.HasOpenPotionSlots,
            ["potionSlotCount"] = player.PotionSlots.Count,
        };

        try { obj["deck"] = CaptureDeck(player); } catch { obj["deck"] = new JsonArray(); }
        try { obj["relics"] = CaptureRelics(player); } catch { obj["relics"] = new JsonArray(); }
        try { obj["potions"] = CapturePotions(player); } catch { obj["potions"] = new JsonArray(); }

        var combatState = player.PlayerCombatState;
        if (combatState != null) {
            try { obj["combat"] = CaptureCombat(player, combatState); }
            catch { }
        }

        var room = state.CurrentRoom;
        if (room != null)
            obj["roomType"] = room.RoomType.ToString();

        EnrichPhase(obj, state, player, phase);
        return obj;
    }

    public static JsonObject CaptureCombatAfter(Player player) {
        var obj = new JsonObject {
            ["playerPowers"] = player.Creature != null
                ? CapturePowers(player.Creature.Powers)
                : new JsonArray(),
        };

        var cs = CombatManager.Instance?.DebugOnlyGetState();
        if (cs != null)
            obj["enemies"] = CaptureEnemies(cs);
        return obj;
    }

    static JsonArray CaptureDeck(Player player) {
        var arr = new JsonArray();
        foreach (var c in player.Deck.Cards) {
            try { arr.Add(LeanCardJson.FromCard(c)); }
            catch { }
        }
        return arr;
    }

    static JsonArray CaptureRelics(Player player) {
        var arr = new JsonArray();
        foreach (var r in player.Relics) {
            arr.Add(new JsonObject {
                ["id"] = SafeRelicId(r),
                ["name"] = SafeRelicTitle(r),
            });
        }
        return arr;
    }

    static JsonArray CapturePotions(Player player) {
        var arr = new JsonArray();
        var slots = player.PotionSlots;
        for (int slot = 0; slot < slots.Count; slot++) {
            var p = slots[slot];
            if (p == null) continue;
            arr.Add(new JsonObject {
                ["id"] = p.Id.Entry ?? "",
                ["slot"] = slot,
            });
        }
        return arr;
    }

    static JsonObject CaptureCombat(Player player, PlayerCombatState combatState) {
        var isPlayPhase = Sts2CombatCompat.IsCombatPlayPhaseActive();
        var cs = CombatManager.Instance?.DebugOnlyGetState();
        var combat = new JsonObject {
            ["maxEnergy"] = player.MaxEnergy,
            ["currentEnergy"] = combatState.Energy,
            ["drawPileCount"] = combatState.DrawPile?.Cards.Count() ?? 0,
            ["discardPileCount"] = combatState.DiscardPile?.Cards.Count() ?? 0,
            ["isPlayPhaseActive"] = isPlayPhase,
            ["phase"] = isPlayPhase ? "PlayPhase" : "NotPlayPhase",
            ["playerBlock"] = player.Creature?.Block ?? 0,
            ["turnNumber"] = cs?.RoundNumber ?? 1,
            ["playerPowers"] = player.Creature != null
                ? CapturePowers(player.Creature.Powers)
                : new JsonArray(),
        };

        var hand = new JsonArray();
        if (combatState.Hand?.Cards != null) {
            var i = 0;
            foreach (var c in combatState.Hand.Cards) {
                try {
                    var cardObj = LeanCardJson.FromCard(c, i);
                    cardObj["canPlay"] = TryCanPlay(c, combatState.Energy);
                    hand.Add(cardObj);
                }
                catch { }
                i++;
            }
        }
        combat["hand"] = hand;

        if (cs != null)
            combat["enemies"] = CaptureEnemies(cs);

        return combat;
    }

    static bool TryCanPlay(CardModel card, int energy) {
        try {
            return card.CanPlay(out _, out _);
        }
        catch {
            return LeanCardJson.ResolveEnergyCost(card) <= energy;
        }
    }

    static JsonArray CapturePowers(IEnumerable<PowerModel?> powers) {
        var arr = new JsonArray();
        foreach (var power in powers) {
            if (power == null) continue;
            var entry = new JsonObject {
                ["id"] = power.GetType().Name,
                ["amount"] = power.Amount,
            };
            try {
                var modelId = power.Id.Entry;
                if (!string.IsNullOrWhiteSpace(modelId))
                    entry["modelId"] = modelId;
            }
            catch { }
            arr.Add(entry);
        }
        return arr;
    }

    static JsonArray CaptureEnemies(MegaCrit.Sts2.Core.Combat.CombatState cs) {
        var arr = new JsonArray();
        var targets = cs.PlayerCreatures.ToList();
        var index = 0;
        foreach (var enemy in cs.Enemies) {
            try {
                var obj = new JsonObject {
                    ["index"] = index++,
                    ["currentHp"] = enemy.CurrentHp,
                    ["maxHp"] = enemy.MaxHp,
                    ["block"] = enemy.Block,
                    ["isAlive"] = enemy.IsAlive,
                };
                try {
                    var monsterId = enemy.ModelId.Entry;
                    if (!string.IsNullOrWhiteSpace(monsterId))
                        obj["monsterId"] = monsterId;
                }
                catch { }
                try {
                    var title = enemy.Monster?.Title?.GetFormattedText();
                    if (!string.IsNullOrWhiteSpace(title))
                        obj["name"] = title;
                }
                catch { }

                if (enemy.Monster?.NextMove != null) {
                    obj["nextMoveId"] = enemy.Monster.NextMove.Id;
                    var intents = new JsonArray();
                    int intentDamage = 0;
                    foreach (var intent in enemy.Monster.NextMove.Intents) {
                        try { intents.Add(intent.ToString()); }
                        catch { intents.Add(intent.IntentType.ToString()); }
                        if (intent.IntentType == IntentType.Hidden) continue;
                        if (intent is AttackIntent attack) {
                            try { intentDamage += attack.GetTotalDamage(targets, enemy); }
                            catch { }
                        }
                    }
                    obj["intents"] = intents;
                    obj["intentDamage"] = intentDamage;
                }

                obj["powers"] = CapturePowers(enemy.Powers);
                arr.Add(obj);
            }
            catch { }
        }
        return arr;
    }

    static void EnrichPhase(JsonObject obj, RunState state, Player player, GamePhase phase) {
        switch (phase) {
            case GamePhase.MapSelection:
                CaptureMapNodes(obj, state);
                break;
            case GamePhase.CardReward:
                CaptureCardReward(obj);
                break;
            case GamePhase.EventChoice:
                CaptureEventChoice(obj);
                break;
            case GamePhase.Shop:
                CaptureShopOffers(obj, state);
                break;
            case GamePhase.RestSite:
                CaptureRestOptions(obj);
                break;
            case GamePhase.RewardScreen:
                CaptureRewardsScreen(obj, player);
                break;
            case GamePhase.RelicSelection:
                CaptureRelicSelection(obj);
                break;
        }
    }

    static void CaptureRewardsScreen(JsonObject obj, Player player) {
        if (NOverlayStack.Instance?.Peek() is not NRewardsScreen screen)
            return;
        obj["rewardsHaveCollectable"] = GameOverlayPhase.HasClickableRewards(
            screen, player.HasOpenPotionSlots);
    }

    static void CaptureMapNodes(JsonObject obj, RunState state) {
        var mapScreen = NMapScreen.Instance;
        if (mapScreen == null || !mapScreen.IsOpen) return;

        var allPoints = GameUi.FindAll<NMapPoint>((Node)mapScreen);
        var available = GetAvailableMapPoints(state, allPoints);
        var arr = new JsonArray();
        for (int i = 0; i < available.Count; i++) {
            var mp = available[i];
            arr.Add(new JsonObject {
                ["index"] = i,
                ["pointType"] = mp.Point.PointType.ToString(),
                ["row"] = mp.Point.coord.row,
                ["col"] = mp.Point.coord.col,
            });
        }
        obj["mapNodes"] = arr;
    }

    static List<NMapPoint> GetAvailableMapPoints(RunState state, List<NMapPoint> allPoints) {
        if (state.VisitedMapCoords.Count == 0)
            return allPoints.Where(mp => mp.Point.coord.row == 0).ToList();

        var visited = state.VisitedMapCoords;
        var lastCoord = visited[visited.Count - 1];
        var lastPoint = allPoints.FirstOrDefault(mp => mp.Point.coord.Equals(lastCoord));
        if (lastPoint == null) return [];

        var childCoords = new HashSet<MapCoord>(lastPoint.Point.Children.Select(c => c.coord));
        return allPoints.Where(mp => childCoords.Contains(mp.Point.coord)).ToList();
    }

    static void CaptureCardReward(JsonObject obj) {
        var screen = GameOverlayPhase.FindCardRewardScreen();
        if (screen == null) return;
        var holders = GameUi.FindAll<NCardHolder>(screen)
            .Where(h => GodotObject.IsInstanceValid(h) && h.Visible && h.CardModel != null)
            .ToList();
        var arr = new JsonArray();
        for (int i = 0; i < holders.Count; i++)
            arr.Add(LeanCardJson.FromCard(holders[i].CardModel!, i));
        obj["offeredCards"] = arr;
    }

    static void CaptureEventChoice(JsonObject obj) {
        var tree = Engine.GetMainLoop() as SceneTree;
        var eventRoom = tree?.Root?.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer/EventRoom");
        if (eventRoom == null) return;

        var buttons = GameUi.FindAll<NEventOptionButton>(eventRoom)
            .Where(b => GodotObject.IsInstanceValid(b))
            .ToList();
        var arr = new JsonArray();
        for (int i = 0; i < buttons.Count; i++) {
            var opt = buttons[i].Option;
            arr.Add(new JsonObject {
                ["index"] = i,
                ["title"] = opt?.Title?.GetFormattedText() ?? "",
                ["locked"] = opt?.IsLocked ?? false,
            });
        }
        obj["eventOptions"] = arr;
    }

    static void CaptureShopOffers(JsonObject obj, RunState state) {
        if (state.CurrentRoom is not MerchantRoom merchantRoom) return;
        var inventory = merchantRoom.GetLocalInventory();
        if (inventory == null) return;

        var arr = new JsonArray();
        int idx = 0;
        var cardEntries = inventory.CharacterCardEntries.Concat(inventory.ColorlessCardEntries);
        foreach (var entry in cardEntries.Where(e => e.IsStocked)) {
            var card = entry.CreationResult?.Card;
            if (card == null) continue;
            var offer = LeanCardJson.FromCard(card, idx);
            offer["offerType"] = "card";
            offer["cost"] = entry.Cost;
            offer["enoughGold"] = entry.EnoughGold;
            arr.Add(offer);
            idx++;
        }

        foreach (var entry in inventory.RelicEntries.Where(e => e.IsStocked && e.Model != null)) {
            arr.Add(new JsonObject {
                ["index"] = idx++,
                ["offerType"] = "relic",
                ["id"] = entry.Model!.Id.Entry ?? "",
                ["name"] = entry.Model.Title.GetFormattedText(),
                ["cost"] = entry.Cost,
                ["enoughGold"] = entry.EnoughGold,
            });
        }

        foreach (var entry in inventory.PotionEntries.Where(e => e.IsStocked)) {
            arr.Add(new JsonObject {
                ["index"] = idx++,
                ["offerType"] = "potion",
                ["id"] = entry.Model?.Id.Entry ?? "",
                ["cost"] = entry.Cost,
                ["enoughGold"] = entry.EnoughGold,
            });
        }

        obj["shopOffers"] = arr;
    }

    static void CaptureRestOptions(JsonObject obj) {
        var tree = Engine.GetMainLoop() as SceneTree;
        var room = tree?.Root?.GetNodeOrNull<NRestSiteRoom>(
            "/root/Game/RootSceneContainer/Run/RoomContainer/RestSiteRoom");
        if (room == null) return;

        var arr = new JsonArray();
        var liveOptions = room.Options;
        for (int i = 0; i < liveOptions.Count; i++) {
            var opt = liveOptions[i];
            arr.Add(new JsonObject {
                ["index"] = i,
                ["optionId"] = opt.OptionId,
                ["title"] = opt.Title.GetFormattedText(),
                ["enabled"] = opt.IsEnabled,
            });
        }
        obj["restOptions"] = arr;
    }

    static void CaptureRelicSelection(JsonObject obj) {
        var screen = GameOverlayPhase.FindRelicSelectionScreen();
        if (screen == null) return;
        var entries = GameUi.FindAll<NRelicCollectionEntry>((Node)screen)
            .Where(e => e.Visible).ToList();
        var arr = new JsonArray();
        for (int i = 0; i < entries.Count; i++) {
            var relic = entries[i].relic;
            if (relic == null) continue;
            arr.Add(new JsonObject {
                ["index"] = i,
                ["id"] = relic.Id.Entry ?? "",
                ["name"] = relic.Title.GetFormattedText(),
            });
        }
        obj["offeredRelics"] = arr;
    }

    static string SafeRelicId(RelicModel relic) {
        try { return relic.Id.Entry ?? ""; }
        catch { return ""; }
    }

    static string SafeRelicTitle(RelicModel relic) {
        try { return relic.Title.GetFormattedText(); }
        catch { return ""; }
    }
}
