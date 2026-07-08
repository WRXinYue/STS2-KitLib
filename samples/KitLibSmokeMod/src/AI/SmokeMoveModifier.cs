using System.Text.Json.Nodes;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;

namespace KitLibSmokeMod.AI;

internal sealed class SmokeMoveModifier : IAiMoveModifier {
    public static SmokeMoveModifier Instance { get; } = new();

    public bool AppliesTo(string? characterId) =>
        string.Equals(characterId, SmokeAiState.CharacterId, StringComparison.OrdinalIgnoreCase);

    public int ModifyScore(JsonObject snapshot, GameAction move, int baseScore) => 0;
}
