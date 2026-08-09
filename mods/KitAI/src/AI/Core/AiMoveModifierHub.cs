using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;
using KitLib.Host;

namespace KitLib.AI.Core;

public static class AiMoveModifierHub {
    public static void Register(IAiMoveModifier modifier) =>
        KitLibHost.RegisterMoveModifier(modifier);

    public static int ApplyModifiers(JsonObject snapshot, GameAction move, int baseScore) {
        var characterId = snapshot["characterId"]?.GetValue<string>();
        return KitLibHost.ApplyMoveModifiers(snapshot, move, baseScore, characterId);
    }
}
