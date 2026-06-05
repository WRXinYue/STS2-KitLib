using System.Collections.Generic;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Planning;

public sealed class DeckPlan {
    public float ThinPreference { get; init; }
    public int TargetDeckSize { get; init; } = 18;
    public IReadOnlyDictionary<AiTag, float> Weights { get; init; } =
        new Dictionary<AiTag, float>();

    public float GetWeight(AiTag tag) =>
        Weights.TryGetValue(tag, out var w) ? w : 0f;

    public sealed class Builder {
        public float ThinPreference { get; set; }
        public int TargetDeckSize { get; set; } = 18;
        public Dictionary<AiTag, float> Weights { get; } = new();

        public void AddWeight(AiTag tag, float delta) {
            Weights.TryGetValue(tag, out var current);
            Weights[tag] = current + delta;
        }

        public DeckPlan Build() => new() {
            ThinPreference = ThinPreference,
            TargetDeckSize = TargetDeckSize,
            Weights = Weights,
        };
    }
}
