using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace KitLib.Mcp.Tools;

internal sealed class DevTagSaveSlotTool : IMcpTool {
    public string Name => "dev_tag_save_slot";
    public string Description =>
        "Set debug notes on a save slot so agents can identify what the save is for.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "slot_id": {
                "type": "integer",
                "description": "Save slot ID (0 = quick save)."
            },
            "notes": {
                "type": "string",
                "description": "Debug label, e.g. combat:ironclad-act1-boss."
            }
        },
        "required": ["slot_id", "notes"]
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        if (!args.TryGetPropertyValue("slot_id", out var slotNode)
            || slotNode?.GetValueKind() != System.Text.Json.JsonValueKind.Number) {
            return Task.FromResult<JsonNode>(Error("Missing or invalid slot_id."));
        }

        var notes = args.TryGetPropertyValue("notes", out var notesNode)
            && notesNode?.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? notesNode.GetValue<string>() ?? ""
            : notesNode?.ToJsonString().Trim('"') ?? "";

        int slotId = slotNode!.GetValue<int>();
        if (!SaveSlotManager.HasSlot(slotId)) {
            return Task.FromResult<JsonNode>(new JsonObject {
                ["ok"] = false,
                ["error"] = $"Slot {slotId} has no save data.",
            });
        }

        if (!SaveSlotManager.SetDebugNotes(slotId, notes)) {
            return Task.FromResult<JsonNode>(new JsonObject {
                ["ok"] = false,
                ["error"] = $"Failed to write debug notes for slot {slotId}.",
            });
        }

        return Task.FromResult<JsonNode>(new JsonObject {
            ["ok"] = true,
            ["slotId"] = slotId,
            ["notes"] = notes,
        });
    }

    private static JsonObject Error(string message) => new() {
        ["ok"] = false,
        ["error"] = message,
    };
}
