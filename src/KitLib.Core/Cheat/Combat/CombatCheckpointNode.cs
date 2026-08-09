using System.Collections.Generic;

namespace KitLib.Combat;

internal sealed class CombatCheckpointNode {
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Source { get; set; } = "auto";
    public int Round { get; set; }
    public string Label { get; set; } = "";
    public long SaveTime { get; set; }
}

internal sealed class CombatCheckpointIndex {
    public long SessionStartedAt { get; set; }
    public List<CombatCheckpointNode> Nodes { get; set; } = new();
}
