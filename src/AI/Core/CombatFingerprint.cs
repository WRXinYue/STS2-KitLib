using System.Text;
using System.Text.Json.Nodes;

namespace DevMode.AI.Core;

internal static class CombatFingerprint {
    internal static string FromSnapshot(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        if (combat == null) return "";

        var sb = new StringBuilder();
        sb.Append('e').Append(combat["currentEnergy"]?.GetValue<int>() ?? -1);
        sb.Append('b').Append(combat["playerBlock"]?.GetValue<int>() ?? 0);

        var hand = combat["hand"]?.AsArray();
        if (hand == null) return sb.ToString();

        sb.Append('h').Append(hand.Count);
        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            sb.Append('|');
            sb.Append(card["id"]?.GetValue<string>() ?? "?");
            sb.Append('@').Append(card["cost"]?.GetValue<int>() ?? -1);
            sb.Append(card["canPlay"]?.GetValue<bool>() == true ? '1' : '0');
        }

        return sb.ToString();
    }
}
