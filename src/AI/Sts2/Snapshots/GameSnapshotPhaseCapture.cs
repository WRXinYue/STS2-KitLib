using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;
using KitLib.AI.Planning;
using KitLib.AI.Knowledge;
using KitLib.AI.Sts2.Helpers;
using KitLib.Actions;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.AI.Sts2.Snapshots;

/// <summary>Captures overlay / room UI context for non-combat AI decisions.</summary>
internal static class GameSnapshotPhaseCapture
{
    public static void Enrich(JsonObject obj, RunState state, Player player, GamePhase phase)
    {
        switch (phase) {
            case GamePhase.EventChoice:
                CaptureEventChoice(obj, state);
                break;
            case GamePhase.RelicSelection:
                CaptureRelicSelection(obj, state);
                break;
            case GamePhase.CardReward:
                CaptureCardReward(obj, state, player);
                break;
            case GamePhase.MapSelection:
                CaptureMapNodes(obj, state);
                break;
            case GamePhase.Shop:
                CaptureShopOffers(obj, state, player);
                break;
            case GamePhase.RestSite:
                CaptureRestOptions(obj);
                break;
            case GamePhase.RewardScreen:
                CaptureRewardsScreen(obj, player);
                break;
        }
    }

    static void CaptureRewardsScreen(JsonObject obj, Player player) {
        if (Engine.GetMainLoop() is not SceneTree tree)
            return;
        if (NOverlayStack.Instance?.Peek() is not NRewardsScreen screen)
            return;

        obj["rewardsHaveCollectable"] = OverlayPhaseHelper.HasClickableRewards(
            screen, player.HasOpenPotionSlots, obj);
    }

    static void CaptureEventChoice(JsonObject obj, RunState state)
    {
        TryCaptureEventId(obj, state);

        var tree = Engine.GetMainLoop() as SceneTree;
        var eventRoom = tree?.Root?.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer/EventRoom")
            ?? tree?.Root?.GetNodeOrNull("Game/RootSceneContainer/Run/RoomContainer/EventRoom");
        if (eventRoom == null)
            return;

        var buttons = UIHelperFindAll<NEventOptionButton>(eventRoom)
            .Where(b => GodotObject.IsInstanceValid(b))
            .ToList();

        var arr = new JsonArray();
        for (int i = 0; i < buttons.Count; i++) {
            var btn = buttons[i];
            var opt = btn.Option;
            var entry = new JsonObject {
                ["index"] = i,
                ["title"] = opt?.Title?.GetFormattedText() ?? "",
                ["textKey"] = TryGetTextKey(opt),
                ["modelId"] = TryGetOptionModelId(opt),
                ["locked"] = opt?.IsLocked ?? false,
            };
            EventOptionInfer.FillOptionKey(entry);
            arr.Add(entry);
        }

        obj["eventOptions"] = arr;

        if (obj["eventId"] == null && InferNeow(arr))
            obj["eventId"] = "EVENT.NEOW";
    }

    static void CaptureRelicSelection(JsonObject obj, RunState state)
    {
        var screen = OverlayPhaseHelper.FindRelicSelectionScreen();
        if (screen == null)
            return;

        var entries = UIHelperFindAll<NRelicCollectionEntry>(screen)
            .Where(e => GodotObject.IsInstanceValid(e) && e.Visible)
            .ToList();

        var arr = new JsonArray();
        for (int i = 0; i < entries.Count; i++) {
            var entry = entries[i];
            var relic = entry.relic;
            if (relic == null) continue;

            arr.Add(new JsonObject {
                ["index"] = i,
                ["id"] = SafeModelId(relic),
                ["name"] = relic.Title.GetFormattedText(),
                ["rarity"] = SafeRarity(relic),
            });
        }

        obj["offeredRelics"] = arr;
        obj["relicChoiceContext"] = InferRelicChoiceContext(obj, state);
    }

    static string InferRelicChoiceContext(JsonObject obj, RunState state) {
        if (state.CurrentRoom is EventRoom)
            return "event";

        var roomType = state.CurrentRoom?.RoomType.ToString() ?? "";
        if (roomType.Contains("Boss", StringComparison.OrdinalIgnoreCase)
            || roomType.Contains("Monster", StringComparison.OrdinalIgnoreCase)
            || roomType.Contains("Elite", StringComparison.OrdinalIgnoreCase))
            return "combat_reward";

        var eventId = obj["eventId"]?.GetValue<string>() ?? "";
        if (eventId.Contains("NEOW", StringComparison.OrdinalIgnoreCase))
            return "event";

        return "combat_reward";
    }

    static void CaptureCardReward(JsonObject obj, RunState state, Player player)
    {
        var screen = OverlayPhaseHelper.FindCardRewardScreen();
        if (screen == null) return;

        var holders = UIHelperFindAll<NCardHolder>(screen)
            .Where(h => GodotObject.IsInstanceValid(h) && h.Visible && h.CardModel != null)
            .ToList();

        var arr = new JsonArray();
        for (int i = 0; i < holders.Count; i++) {
            var card = holders[i].CardModel!;
            arr.Add(SnapshotCardJson.FromCard(card, i));
        }

        obj["offeredCards"] = arr;

        obj["deckSelectContext"] = screen switch {
            NDeckCardSelectScreen => state.CurrentRoom?.RoomType switch {
                RoomType.RestSite => "upgrade",
                RoomType.Shop => "remove",
                _ => "deckPick",
            },
            NChooseACardSelectionScreen => "choose",
            _ => "reward",
        };

        CaptureNextFightPreview(obj, state, player);
    }

    static void CaptureNextFightPreview(JsonObject obj, RunState state, Player player) {
        var route = NextFightRoute.Resolve(state, player);
        if (route.Count == 0)
            return;

        var preview = new JsonArray();
        foreach (var fight in route) {
            var enemyArr = new JsonArray();
            foreach (var enemy in fight.Enemies) {
                enemyArr.Add(new JsonObject {
                    ["index"] = enemy.Index,
                    ["monsterId"] = enemy.MonsterId,
                    ["hp"] = enemy.CurrentHp,
                    ["intentDamage"] = enemy.IntentDamage,
                    ["nonDamageThreat"] = enemy.NonDamageThreat,
                    ["isMinion"] = enemy.IsMinion,
                });
            }

            preview.Add(new JsonObject {
                ["roomType"] = fight.RoomType.ToString(),
                ["encounterId"] = fight.EncounterId,
                ["weight"] = fight.Weight,
                ["incomingTurn1"] = fight.IncomingTurn1,
                ["enemies"] = enemyArr,
            });
        }

        obj["nextFightPreview"] = preview;
    }

    static void CaptureMapNodes(JsonObject obj, RunState state)
    {
        var mapScreen = NMapScreen.Instance;
        if (mapScreen == null || !mapScreen.IsOpen) return;

        var allPoints = UIHelperFindAll<NMapPoint>((Node)mapScreen);
        var available = GetAvailableMapPoints(state, allPoints);
        var arr = new JsonArray();

        for (int i = 0; i < available.Count; i++) {
            var mp = available[i];
            var pointType = mp.Point.PointType.ToString();
            arr.Add(new JsonObject {
                ["index"] = i,
                ["pointType"] = pointType,
                ["row"] = mp.Point.coord.row,
                ["col"] = mp.Point.coord.col,
                ["isCombat"] = MapEncounterPreview.IsCombatNode(mp.Point.PointType),
            });
        }

        obj["mapNodes"] = arr;
    }

    static List<NMapPoint> GetAvailableMapPoints(RunState state, List<NMapPoint> allPoints)
    {
        if (state.VisitedMapCoords.Count == 0)
            return allPoints.Where(mp => mp.Point.coord.row == 0).ToList();

        var visited = state.VisitedMapCoords;
        var lastCoord = visited[visited.Count - 1];
        var lastPoint = allPoints.FirstOrDefault(mp => mp.Point.coord.Equals(lastCoord));
        if (lastPoint == null) return [];

        var childCoords = new HashSet<MapCoord>(lastPoint.Point.Children.Select(c => c.coord));
        return allPoints.Where(mp => childCoords.Contains(mp.Point.coord)).ToList();
    }

    static void CaptureShopOffers(JsonObject obj, RunState state, Player player)
    {
        if (state.CurrentRoom is not MerchantRoom merchantRoom) return;

        var inventory = merchantRoom.Inventory;
        if (inventory == null) return;

        var arr = new JsonArray();
        int idx = 0;

        foreach (var entry in inventory.CardEntries.Where(e => e.IsStocked)) {
            var card = entry.CreationResult?.Card;
            if (card == null) continue;
            var offer = SnapshotCardJson.FromCard(card, idx);
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
                ["rarity"] = entry.Model.Rarity.ToString(),
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

        var removal = inventory.CardRemovalEntry;
        if (removal is { IsStocked: true }) {
            arr.Add(new JsonObject {
                ["index"] = idx,
                ["offerType"] = "removeCard",
                ["cost"] = removal.Cost,
                ["enoughGold"] = removal.EnoughGold,
            });
        }

        obj["shopOffers"] = arr;
    }

    static void CaptureRestOptions(JsonObject obj)
    {
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

        try {
            obj["restProceedReady"] = room.ProceedButton is { IsEnabled: true };
        }
        catch {
            obj["restProceedReady"] = false;
        }
    }

    static void TryCaptureEventId(JsonObject obj, RunState state)
    {
        if (state.CurrentRoom is not EventRoom eventRoom)
            return;

        try {
            var modelProp = AccessTools.Property(typeof(EventRoom), "Model")
                ?? AccessTools.Property(typeof(EventRoom), "EventModel");
            if (modelProp?.GetValue(eventRoom) is EventModel model)
                obj["eventId"] = model.Id.Entry ?? "";
        }
        catch {
            // Best-effort only.
        }
    }

    static string TryGetTextKey(object? option) {
        if (option == null) return "";
        try {
            var prop = option.GetType().GetProperty("TextKey")
                ?? option.GetType().GetProperty("LocalizationKey");
            return prop?.GetValue(option)?.ToString() ?? "";
        }
        catch {
            return "";
        }
    }

    static string TryGetOptionModelId(object? option) {
        if (option == null) return "";
        try {
            var idProp = option.GetType().GetProperty("Id")
                ?? option.GetType().GetProperty("ModelId");
            var val = idProp?.GetValue(option);
            if (val is ModelId modelId)
                return modelId.Entry ?? "";
            return val?.ToString() ?? "";
        }
        catch {
            return "";
        }
    }

    static bool InferNeow(JsonArray options) {
        foreach (var node in options) {
            if (node is not JsonObject opt) continue;
            var key = opt["textKey"]?.GetValue<string>() ?? "";
            if (key.Contains("NEOW", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static string SafeModelId(RelicModel relic) {
        try { return relic.Id.Entry ?? ""; }
        catch { return ""; }
    }

    static string SafeRarity(RelicModel relic) {
        try { return relic.Rarity.ToString(); }
        catch { return ""; }
    }

    static List<T> UIHelperFindAll<T>(Node start) where T : Node {
        var list = new List<T>();
        if (!GodotObject.IsInstanceValid(start))
            return list;
        FindRecursive(start, list);
        return list;
    }

    static void FindRecursive<T>(Node node, List<T> list) where T : Node {
        if (node is T match)
            list.Add(match);
        foreach (var child in node.GetChildren()) {
            if (child is Node childNode)
                FindRecursive(childNode, list);
        }
    }
}
