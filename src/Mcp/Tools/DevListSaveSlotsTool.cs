using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace KitLib.Mcp.Tools;

internal sealed class DevListSaveSlotsTool : IMcpTool {
    public string Name => "dev_list_save_slots";
    public string Description =>
        "List DevMode save slots with metadata and debug notes for AI slot selection.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {},
        "required": []
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        var slots = new JsonArray();
        foreach (var slotId in SaveSlotManager.GetAllSlotIds()) {
            if (!SaveSlotManager.HasSlot(slotId))
                continue;
            slots.Add(SerializeSlot(slotId));
        }

        if (SaveSlotManager.HasSlot(SaveSlotManager.QuickSlotId)
            && !SaveSlotManager.GetAllSlotIds().Contains(SaveSlotManager.QuickSlotId)) {
            slots.Add(SerializeSlot(SaveSlotManager.QuickSlotId));
        }

        return Task.FromResult<JsonNode>(new JsonObject { ["slots"] = slots });
    }

    private static JsonObject SerializeSlot(int slotId) {
        var meta = SaveSlotManager.LoadMeta(slotId);
        return new JsonObject {
            ["slotId"] = slotId,
            ["name"] = meta?.DisplayName ?? (slotId == SaveSlotManager.QuickSlotId
                ? SaveSlotManager.QuickSlotDisplayName
                : "Save"),
            ["debugNotes"] = meta?.DebugNotes ?? "",
            ["totalFloor"] = meta?.TotalFloor ?? 0,
            ["characterId"] = meta?.CharacterId ?? "",
            ["hp"] = meta?.Hp ?? 0,
            ["gold"] = meta?.Gold ?? 0,
            ["saveTime"] = meta?.SaveTime ?? 0,
            ["seed"] = meta?.Seed ?? "",
            ["isQuickSlot"] = slotId == SaveSlotManager.QuickSlotId,
        };
    }
}
