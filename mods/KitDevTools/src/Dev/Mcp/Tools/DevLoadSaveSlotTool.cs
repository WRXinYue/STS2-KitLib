using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace KitLib.Mcp.Tools;

internal sealed class DevLoadSaveSlotTool : IMcpTool {
    public string Name => "dev_load_save_slot";
    public string Description =>
        "Load a DevMode save slot. Returns immediately; poll dev_get_session until runActive is true.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "slot_id": {
                "type": "integer",
                "description": "Save slot ID (default 0 = quick save).",
                "default": 0
            }
        },
        "required": []
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        int slotId = 0;
        if (args.TryGetPropertyValue("slot_id", out var slotNode)
            && slotNode?.GetValueKind() == System.Text.Json.JsonValueKind.Number) {
            slotId = slotNode.GetValue<int>();
        }

        if (!SaveSlotManager.HasSlot(slotId)) {
            return Task.FromResult<JsonNode>(new JsonObject {
                ["ok"] = false,
                ["error"] = $"Slot {slotId} has no save data.",
            });
        }

        if (!SaveSlotManager.LoadFromSlot(slotId)) {
            return Task.FromResult<JsonNode>(new JsonObject {
                ["ok"] = false,
                ["error"] = $"Failed to start loading slot {slotId}.",
            });
        }

        return Task.FromResult<JsonNode>(new JsonObject {
            ["ok"] = true,
            ["slotId"] = slotId,
            ["status"] = "loading",
        });
    }
}
