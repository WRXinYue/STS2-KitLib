using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;

namespace KitLib.AI.Core;

public interface IAiMoveModifier {
    bool AppliesTo(string? characterId);
    int ModifyScore(JsonObject snapshot, GameAction move, int baseScore);
}
