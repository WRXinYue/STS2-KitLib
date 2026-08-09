using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI.Core.Schema;
using KitLib.AI.Sts2.Mcp;

namespace KitLib.Mcp.Tools;

internal static class McpCombatActionExtensions {
    public static void ApplyPendingSelection(JsonObject response, string actionStr, ActionResult result) {
        if (actionStr != "play_card" || result.Success)
            return;

        if (result.Message != "pending_selection" && !McpCardSelectionHelper.IsActive())
            return;

        response["pendingSelection"] = true;
        response["selectionState"] = McpCardSelectionHelper.CaptureState();
        if (result.Message == "pending_selection")
            response["message"] =
                "Card play awaiting selection. Call selection_action, then get_game_state to verify.";
    }
}
