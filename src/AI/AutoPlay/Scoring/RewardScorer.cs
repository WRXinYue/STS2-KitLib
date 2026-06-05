using System.Text.Json.Nodes;
using DevMode.AI.Core.Schema;

namespace DevMode.AI.AutoPlay.Scoring;

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
