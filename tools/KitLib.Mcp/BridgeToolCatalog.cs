using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace KitLib.McpProxy;

/// <summary>
/// Loads the tool list from the running game, falling back to a cached copy so the IDE still sees
/// the tools while the game is closed.
/// </summary>
internal static class BridgeToolCatalog {
    private static string CachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KitLib.Mcp",
        "tools.json");

    public static async Task<IReadOnlyList<McpServerTool>> LoadAsync(
        GameBridgeClient bridge,
        CancellationToken cancellationToken = default) {
        JsonArray tools;
        try {
            tools = await bridge.ListToolsAsync(cancellationToken);
            SaveCache(tools);
        }
        catch {
            // Game not running: serve the last known schema so the client can still list tools.
            tools = LoadCache() ?? new JsonArray();
        }

        return Build(bridge, tools);
    }

    private static List<McpServerTool> Build(GameBridgeClient bridge, JsonArray tools) {
        var result = new List<McpServerTool>(tools.Count);
        foreach (var node in tools) {
            if (node is not JsonObject tool)
                continue;

            string? name = tool["name"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrEmpty(name) || name == "dev_launch_game" || name == "dev_wait_bridge" ||
                name == "dev_stop_game")
                continue;

            var protocolTool = new Tool {
                Name = name,
                Description = tool["description"]?.GetValue<string>(),
                InputSchema = tool["inputSchema"] != null
                    ? JsonSerializer.SerializeToElement(tool["inputSchema"])
                    : JsonSerializer.SerializeToElement(new JsonObject { ["type"] = "object" }),
            };
            result.Add(new BridgeMcpServerTool(bridge, protocolTool));
        }

        return result;
    }

    private static void SaveCache(JsonArray tools) {
        try {
            string path = CachePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, tools.ToJsonString());
        }
        catch {
            // Cache is best-effort only.
        }
    }

    private static JsonArray? LoadCache() {
        try {
            string path = CachePath;
            if (!File.Exists(path))
                return null;
            return JsonNode.Parse(File.ReadAllText(path)) as JsonArray;
        }
        catch {
            return null;
        }
    }
}
