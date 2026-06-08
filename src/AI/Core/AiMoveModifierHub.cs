using System.Collections.Generic;
using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;

namespace KitLib.AI.Core;

public static class AiMoveModifierHub {
    static readonly List<IAiMoveModifier> Modifiers = [];

    public static void Register(IAiMoveModifier modifier) {
        Modifiers.Add(modifier);
        MainFile.Logger.Info($"[AiSim] Move modifier registered type={modifier.GetType().Name}.");
    }

    public static int ApplyModifiers(JsonObject snapshot, GameAction move, int baseScore) {
        var characterId = snapshot["characterId"]?.GetValue<string>();
        var score = baseScore;
        foreach (var modifier in Modifiers) {
            if (!modifier.AppliesTo(characterId)) continue;
            try {
                score += modifier.ModifyScore(snapshot, move, baseScore);
            }
            catch (System.Exception ex) {
                MainFile.Logger.Warn($"[AiScorer] Modifier {modifier.GetType().Name} failed: {ex.Message}");
            }
        }

        return score;
    }
}
