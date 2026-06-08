using System.Collections.Generic;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

public sealed class DeckPlan {
    public float ThinPreference { get; init; }
    public int TargetDeckSize { get; init; } = 18;
    /// <summary>Ideal strike count; surplus drives removal priority.</summary>
    public int TargetStrikeCount { get; init; } = 1;
    public int TargetDefendCount { get; init; } = 2;
    /// <summary>Non-starter block cards (cost ≤2) needed for path survivability.</summary>
    public int TargetBlockSources { get; init; } = 2;
    public int TargetDrawSources { get; init; } = 2;
    public IReadOnlyDictionary<AiTag, float> Weights { get; init; } =
        new Dictionary<AiTag, float>();

    public float GetWeight(AiTag tag) =>
        Weights.TryGetValue(tag, out var w) ? w : 0f;

    public bool IsExhaustFocused => GetWeight(AiTag.Exhaust) >= 1f;

    public sealed class Builder {
        public float ThinPreference { get; set; }
        public int TargetDeckSize { get; set; } = 18;
        public int TargetStrikeCount { get; set; } = 1;
        public int TargetDefendCount { get; set; } = 2;
        public int TargetBlockSources { get; set; } = 2;
        public int TargetDrawSources { get; set; } = 2;
        public Dictionary<AiTag, float> Weights { get; } = new();

        public void AddWeight(AiTag tag, float delta) {
            Weights.TryGetValue(tag, out var current);
            Weights[tag] = current + delta;
        }

        public DeckPlan Build() => new() {
            ThinPreference = ThinPreference,
            TargetDeckSize = TargetDeckSize,
            TargetStrikeCount = TargetStrikeCount,
            TargetDefendCount = TargetDefendCount,
            TargetBlockSources = TargetBlockSources,
            TargetDrawSources = TargetDrawSources,
            Weights = Weights,
        };
    }
}
