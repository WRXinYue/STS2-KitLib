using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace KitLib.Mcp.Tools;

internal sealed class McpToolRegistry {
    private readonly Dictionary<string, IMcpTool> _tools = new();

    public void Register(IMcpTool tool) => _tools[tool.Name] = tool;

    public IEnumerable<string> ListToolNames() => _tools.Keys;

    public JsonArray ListToolSchemas() {
        var arr = new JsonArray();
        foreach (var tool in _tools.Values) {
            arr.Add(new JsonObject {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = JsonNode.Parse(tool.InputSchemaJson),
            });
        }
        return arr;
    }

    public async Task<JsonNode> CallAsync(string name, JsonObject? args) {
        if (!_tools.TryGetValue(name, out var tool)) {
            return new JsonObject {
                ["content"] = new JsonArray {
                    new JsonObject {
                        ["type"] = "text",
                        ["text"] = $"Unknown tool: {name}",
                    }
                },
                ["isError"] = true,
            };
        }

        var result = await tool.ExecuteAsync(args ?? new JsonObject());
        return new JsonObject {
            ["content"] = new JsonArray {
                new JsonObject {
                    ["type"] = "text",
                    ["text"] = result.ToJsonString(),
                }
            },
        };
    }
}

internal interface IMcpTool {
    string Name { get; }
    string Description { get; }
    string InputSchemaJson { get; }
    Task<JsonNode> ExecuteAsync(JsonObject args);
}
