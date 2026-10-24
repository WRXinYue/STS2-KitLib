using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace KitLib.Mcp.Tools;

/// <summary>Runs several tool calls in one request, so a setup sequence costs one round trip instead
/// of one per call.</summary>
internal sealed class DevBatchTool : IMcpTool {
    private const int MaxCalls = 20;

    private readonly McpToolRegistry _registry;

    public DevBatchTool(McpToolRegistry registry) => _registry = registry;

    public string Name => "dev_batch";
    public string Description =>
        "Run tool calls in order in a single request, e.g. set energy then play two cards. " +
        "Each entry is {tool, arguments}; nested dev_batch is rejected. At most 20 calls per batch.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "calls": {
                "type": "array",
                "description": "Calls to run in order.",
                "items": {
                    "type": "object",
                    "properties": {
                        "tool": { "type": "string" },
                        "arguments": { "type": "object" }
                    },
                    "required": ["tool"]
                }
            },
            "stop_on_error": {
                "type": "boolean",
                "description": "Stop at the first failing call (default false)."
            }
        },
        "required": ["calls"]
    }
    """;

    public async Task<JsonNode> ExecuteAsync(JsonObject args) {
        if (args["calls"] is not JsonArray calls || calls.Count == 0)
            return new JsonObject { ["ok"] = false, ["error"] = "calls must be a non-empty array." };

        bool stopOnError = args["stop_on_error"]?.GetValue<bool>() ?? false;
        bool truncated = calls.Count > MaxCalls;
        var results = new JsonArray();

        for (int i = 0; i < Math.Min(calls.Count, MaxCalls); i++) {
            if (calls[i] is not JsonObject call)
                continue;

            var name = call["tool"]?.GetValue<string>()?.Trim() ?? "";
            if (string.IsNullOrEmpty(name) || name == Name) {
                results.Add(new JsonObject {
                    ["tool"] = name,
                    ["result"] = new JsonObject { ["ok"] = false, ["error"] = "Invalid tool name." },
                });
                if (stopOnError)
                    break;
                continue;
            }

            var callArgs = call["arguments"] as JsonObject ?? new JsonObject();
            var result = await _registry.ExecuteToolAsync(name, callArgs);
            results.Add(new JsonObject {
                ["tool"] = name,
                ["result"] = result,
            });

            if (stopOnError && Failed(result))
                break;
        }

        return new JsonObject {
            ["ok"] = true,
            ["count"] = results.Count,
            ["truncated"] = truncated,
            ["results"] = results,
        };
    }

    private static bool Failed(JsonNode result) =>
        result is JsonObject obj &&
        (obj["error"] != null || obj["success"]?.GetValue<bool>() == false);
}
