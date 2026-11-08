using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace KitLib.Mcp.Tools;

internal sealed class McpToolRegistry {
    private readonly Dictionary<string, IMcpTool> _tools = new();

    // Read-only tools must not release a deliberate hover (a caller may hover, read the tip, or take a
    // screenshot of the preview). Every other tool advances or mutates the screen, so leaving its hover
    // focused shows a stale preview that no longer belongs to the current UI.
    private static readonly HashSet<string> KeepHoverTools = new(StringComparer.Ordinal) {
        "dev_hover",
        "dev_screenshot",
        "dev_batch",
        "get_game_state",
        "get_map_state",
        "get_selection_state",
        "get_combat_stats",
        "dev_get_session",
        "dev_read_text",
        "dev_list_cards",
        "dev_list_save_slots",
        "dev_list_monsters",
        "dev_list_enemies",
    };

    private static void ReleaseHoverBefore(string toolName) {
        if (!KeepHoverTools.Contains(toolName))
            DevHoverTool.ReleaseHover();
    }

    public int Count => _tools.Count;

    public int CoreCount => _tools.Keys.Count(McpToolCatalog.IsCore);

    public bool Contains(string name) => _tools.ContainsKey(name);

    public void Register(IMcpTool tool) => _tools[tool.Name] = tool;

    public IEnumerable<string> ListToolNames() => _tools.Keys;

    public JsonArray ListToolSchemas(bool coreOnly = false) {
        var arr = new JsonArray();
        foreach (var tool in _tools.Values) {
            if (coreOnly && !McpToolCatalog.IsCore(tool.Name))
                continue;

            var schema = new JsonObject {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = JsonNode.Parse(tool.InputSchemaJson),
            };
            McpToolCatalog.Enrich(schema, tool.Name);
            arr.Add(schema);
        }
        return arr;
    }

    /// <summary>Runs one tool and returns its bare result, for callers such as <c>dev_batch</c> that
    /// compose tools instead of forwarding them to the client.</summary>
    public async Task<JsonNode> ExecuteToolAsync(string name, JsonObject? args) {
        if (!_tools.TryGetValue(name, out var tool))
            return new JsonObject { ["error"] = $"Unknown tool: {name}" };

        ReleaseHoverBefore(name);
        return await tool.ExecuteAsync(args ?? new JsonObject());
    }

    /// <summary>
    /// Runs one tool for an MCP <c>tools/call</c> response. Tool-level failures become visible
    /// <c>isError</c> results (with text) instead of JSON-RPC errors, so clients show the reason to
    /// the model; JSON-RPC errors stay reserved for protocol failures.
    /// </summary>
    public async Task<JsonObject> CallAsync(string name, JsonObject? args) {
        if (!_tools.TryGetValue(name, out var tool))
            return ErrorResult($"Unknown tool: {name}");

        ReleaseHoverBefore(name);
        try {
            // Tools that return MCP content blocks other than a single text block (for example
            // images) build the whole CallToolResult themselves.
            if (tool is IMcpResultTool resultTool)
                return await resultTool.ExecuteResultAsync(args ?? new JsonObject());

            var result = await tool.ExecuteAsync(args ?? new JsonObject());
            return SuccessResult(result);
        }
        catch (Exception ex) {
            return ErrorResult(ex.Message);
        }
    }

    public static JsonObject SuccessResult(JsonNode result) {
        var response = new JsonObject {
            ["content"] = new JsonArray {
                new JsonObject {
                    ["type"] = "text",
                    ["text"] = result.ToJsonString(),
                }
            },
        };

        // structuredContent keeps the value programmatic for clients (DSH canonical values, Claude
        // structured results) instead of forcing a re-parse of the text block.
        if (result is JsonObject structured)
            response["structuredContent"] = structured.DeepClone();

        return response;
    }

    public static JsonObject ErrorResult(string message) => new() {
        ["content"] = new JsonArray {
            new JsonObject {
                ["type"] = "text",
                ["text"] = message,
            }
        },
        ["isError"] = true,
    };
}

internal interface IMcpTool {
    string Name { get; }
    string Description { get; }
    string InputSchemaJson { get; }
    Task<JsonNode> ExecuteAsync(JsonObject args);
}

/// <summary>Tool that builds its own MCP <c>tools/call</c> result, for content blocks such as images
/// that <see cref="McpToolRegistry.SuccessResult"/> cannot express as a single text block.</summary>
internal interface IMcpResultTool : IMcpTool {
    Task<JsonObject> ExecuteResultAsync(JsonObject args);
}
