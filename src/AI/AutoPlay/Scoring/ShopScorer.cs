using System;
using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;

namespace KitLib.AI.AutoPlay.Scoring;

public static class ShopScorer {
    const int MinGoldAfterShopping = 25;

    public static GameAction PickBest(JsonObject snapshot) {
        var offers = snapshot["shopOffers"]?.AsArray();
        var gold = snapshot["gold"]?.GetValue<int>() ?? 0;
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);

        if (offers == null || offers.Count == 0)
            return Leave("No shop data");

        int bestPurchaseIdx = -1;
        int bestPurchaseScore = int.MinValue;
        int removeScore = int.MinValue;
        int purchasableIdx = 0;
        string bestOfferType = "item";
        JsonObject? bestOffer = null;

        for (int i = 0; i < offers.Count; i++) {
            if (offers[i] is not JsonObject offer) continue;
            if (offer["enoughGold"]?.GetValue<bool>() == false) continue;

            var type = offer["offerType"]?.GetValue<string>() ?? "";
            if (type == "removeCard") {
                removeScore = ScoreRemoval(offer, metrics, gold, snapshot);
                continue;
            }

            int score = ScorePurchase(offer, plan, metrics.DeckSize, gold, snapshot);
            if (score > bestPurchaseScore) {
                bestPurchaseScore = score;
                bestPurchaseIdx = purchasableIdx;
                bestOfferType = type;
                bestOffer = offer;
            }
            purchasableIdx++;
        }

        if (removeScore > 0 && removeScore >= bestPurchaseScore) {
            return new GameAction {
                Type = ActionType.RemoveCardAtShop,
                TargetIndex = 0,
                Reason = $"Remove [{metrics.WorstCardName}] uplift={metrics.RemovalUplift} "
                    + $"strikes+{metrics.StrikeSurplus} burnDebt={metrics.CardsNeedingBurn} "
                    + $"score={removeScore} vs buy={bestOfferType}({bestPurchaseScore})",
            };
        }

        if (bestPurchaseIdx >= 0 && bestPurchaseScore > 0) {
            if (bestOfferType == "potion"
                && snapshot["hasOpenPotionSlots"]?.GetValue<bool>() == false
                && bestOffer != null) {
                var potionId = bestOffer["id"]?.GetValue<string>();
                if (PotionInventoryScorer.ShouldMakeRoom(potionId, snapshot, out var discardSlot)) {
                    return new GameAction {
                        Type = ActionType.DiscardPotion,
                        TargetIndex = discardSlot,
                        Reason = $"Discard slot {discardSlot} to buy potion [{potionId}]",
                    };
                }
            }

            return new GameAction {
                Type = ActionType.PurchaseShopItem,
                TargetIndex = bestPurchaseIdx,
                Reason = $"Buy {bestOfferType} score={bestPurchaseScore} "
                    + $"(remove={removeScore}, uplift={metrics.RemovalUplift})",
            };
        }

        return Leave($"Nothing worth buying (remove={removeScore}, buy={bestPurchaseScore}, "
            + $"uplift={metrics.RemovalUplift})");
    }

    static int ScoreRemoval(JsonObject offer, DeckMetrics metrics, int gold, JsonObject snapshot) {
        if (metrics.RemovalUplift < DeckEvaluator.MinRemovalUplift) return 0;

        var cost = offer["cost"]?.GetValue<int>() ?? 999;
        if (gold < cost) return 0;

        int score = metrics.RemovalUplift;
        string? worstId = null;
        var deck = snapshot["deck"]?.AsArray();
        if (deck != null) {
            foreach (var node in deck) {
                if (node is not JsonObject card) continue;
                if ((card["index"]?.GetValue<int>() ?? -1) == metrics.WorstCardIndex) {
                    worstId = card["id"]?.GetValue<string>();
                    break;
                }
            }
        }
        score += CodexPriorCatalog.GetRemoveBonus(snapshot["characterId"]?.GetValue<string>(), worstId);
        score -= cost / 4;
        score -= OpportunityCost(gold, cost, snapshot);
        if (gold >= cost + MinGoldAfterShopping + 50) score += 5;
        if (gold - cost < MinGoldAfterShopping) score -= 25;

        return score;
    }

    static int OpportunityCost(int gold, int cost, JsonObject snapshot) {
        var actIndex = snapshot["actIndex"]?.GetValue<int>() ?? 0;
        int reserve = MinGoldAfterShopping;
        if (actIndex >= 2) reserve += 15;
        if (gold - cost < reserve + 30) return 15;
        if (gold - cost < reserve + 60) return 8;
        return 0;
    }

    static int ScorePurchase(JsonObject offer, DeckPlan plan, int deckSize, int gold, JsonObject snapshot) {
        var type = offer["offerType"]?.GetValue<string>() ?? "";
        var cost = offer["cost"]?.GetValue<int>() ?? 999;
        if (gold - cost < MinGoldAfterShopping) return 0;

        if (type == "potion") {
            var id = offer["id"]?.GetValue<string>() ?? "";
            if (snapshot["hasOpenPotionSlots"]?.GetValue<bool>() == false) {
                if (string.IsNullOrEmpty(id)
                    || !PotionInventoryScorer.ShouldMakeRoom(id, snapshot, out _))
                    return 0;
            }
            return PotionInventoryScorer.ValueOffer(offer, snapshot) - cost / 25;
        }

        return type switch {
            "card" => MacroScorerHelper.ScoreCardOffer(offer, plan, deckSize, snapshot) - cost / 8,
            "relic" => MacroScorerHelper.ScoreRelicOffer(offer, plan, null, snapshot) - cost / 12,
            _ => 0,
        };
    }

    static GameAction Leave(string reason) => new() {
        Type = ActionType.LeaveShop,
        Reason = reason,
    };
}
