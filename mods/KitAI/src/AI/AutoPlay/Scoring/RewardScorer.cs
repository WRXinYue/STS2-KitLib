using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;

namespace KitLib.AI.AutoPlay.Scoring;

public static class RewardScorer {
    public static GameAction PickBest(JsonObject snapshot) {
        if (snapshot["rewardsHaveCollectable"]?.GetValue<bool>() == false)
            return new GameAction {
                Type = ActionType.DismissRewards,
                Reason = "Skip uncollectable rewards",
            };

        return new GameAction {
            Type = ActionType.CollectReward,
            TargetIndex = 0,
            Reason = "Collect first reward",
        };
    }
}
