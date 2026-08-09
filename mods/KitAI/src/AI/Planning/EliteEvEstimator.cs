using System;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.AI.Planning;
/// <summary>Elite / combat node EV: expected rewards minus sim fight cost.</summary>
public static class EliteEvEstimator {
    public static int EstimateNetEv(JsonObject snapshot, DeckPlan plan, NextFightNode fight) {
        int reward = EstimateRewardEv(snapshot, plan, fight.RoomType);
        int cost = EstimateFightCost(snapshot, fight);
        return reward - cost;
    }

    public static int EstimateRewardEv(JsonObject snapshot, DeckPlan plan, RoomType roomType) {
        switch (roomType) {
            case RoomType.Elite:
                int relicEv = DeckCardScoring.RarityScore("RARE");
                int cardEv = Math.Max(6, DeckSimScorer.HasRoutePreview(snapshot) ? 10 : 6);
                return relicEv + cardEv;

            case RoomType.Boss:
                return DeckCardScoring.RarityScore("RARE") + 15;

            case RoomType.Monster: {
                    int gold = snapshot["gold"]?.GetValue<int>() ?? 0;
                    return 6 + (gold < 80 ? 3 : 0);
                }

            default:
                return 4;
        }
    }

    public static int EstimateFightCost(JsonObject snapshot, NextFightNode fight) {
        var estimate = DeckDrawEvEstimator.EstimateCombined(snapshot, null, fight);
        int playerHp = snapshot["currentHp"]?.GetValue<int>() ?? 0;

        int cost = estimate.Outcome.ExpectedFightChip / 3;
        cost += estimate.Outcome.ExpectedChip;
        cost += Math.Min(estimate.Outcome.ExpectedRemainingHp / 4, 30);

        if (estimate.Outcome.ExpectedKillTurns >= 5)
            cost += 12;
        if (estimate.Outcome.ExpectedKillTurns >= FightOutcomeEstimator.UnkillableTurns / 2)
            cost += 25;

        if (playerHp > 0 && estimate.Outcome.ExpectedFightChip >= playerHp)
            cost += 35;
        if (playerHp > 0 && estimate.Outcome.MaxChip >= playerHp)
            cost += 20;

        if (estimate.Outcome.SampleCount > 0) {
            float failRate = 1f - (float)estimate.Outcome.LethalSamples / estimate.Outcome.SampleCount;
            cost += (int)Math.Round(failRate * 15f);
        }

        if (fight.RoomType == RoomType.Elite)
            cost += 4;

        return cost;
    }
}
