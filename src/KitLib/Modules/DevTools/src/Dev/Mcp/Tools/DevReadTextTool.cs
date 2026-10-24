using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace KitLib.Mcp.Tools;

/// <summary>Reads the text of on-screen labels under a node, so verifying a HUD does not require a
/// screenshot and vision round trip.</summary>
internal sealed class DevReadTextTool : IMcpTool {
    private const int DefaultMax = 60;

    public string Name => "dev_read_text";
    public string Description =>
        "Read visible UI text (labels, rich text, button captions) under a node. target is a node name, " +
        "matched exactly first then by case-insensitive substring, e.g. 'CombatRings' for the right-edge " +
        "HUD or 'TopBar' for the header. Cheaper and exact compared with screenshots.";
    public string InputSchemaJson => $$"""
    {
        "type": "object",
        "properties": {
            "target": {
                "type": "string",
                "description": "Node name to read, exact match first, then substring match."
            },
            "max": {
                "type": "integer",
                "description": "Max text entries to return (default {{DefaultMax}}, max 300)."
            }
        },
        "required": ["target"]
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        var target = args["target"]?.GetValue<string>()?.Trim() ?? "";
        if (string.IsNullOrEmpty(target))
            return Task.FromResult<JsonNode>(new JsonObject { ["ok"] = false, ["error"] = "target is required." });

        int max = Math.Clamp(args["max"]?.GetValue<int>() ?? DefaultMax, 1, 300);
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return Task.FromResult<JsonNode>(new JsonObject { ["ok"] = false, ["error"] = "Scene tree unavailable." });

        var matches = FindMatches(tree.Root, target);
        if (matches.Count == 0)
            return Task.FromResult<JsonNode>(new JsonObject { ["ok"] = false, ["error"] = $"No node matches '{target}'." });

        var texts = new JsonArray();
        foreach (var node in matches) {
            Collect(node, texts, max);
            if (texts.Count >= max)
                break;
        }

        return Task.FromResult<JsonNode>(new JsonObject {
            ["ok"] = true,
            ["target"] = target,
            ["matched"] = matches.Count,
            ["count"] = texts.Count,
            ["texts"] = texts,
        });
    }

    // Exact name first: a caller asking for "CombatRings" wants that HUD, not every node whose name
    // happens to contain it. Substring is the fallback for type-like queries such as "NMapPoint".
    private static List<Node> FindMatches(Node root, string target) {
        var exact = new List<Node>();
        var partial = new List<Node>();
        Walk(root, target, exact, partial);
        return exact.Count > 0 ? exact : partial;
    }

    private static void Walk(Node node, string target, List<Node> exact, List<Node> partial) {
        if (node.Name.ToString().Equals(target, StringComparison.OrdinalIgnoreCase))
            exact.Add(node);
        else if (node.Name.ToString().Contains(target, StringComparison.OrdinalIgnoreCase))
            partial.Add(node);

        foreach (var child in node.GetChildren())
            Walk(child, target, exact, partial);
    }

    private static void Collect(Node node, JsonArray texts, int max) {
        if (texts.Count >= max)
            return;
        if (node is CanvasItem canvas && !canvas.IsVisibleInTree())
            return;

        var text = node switch {
            Label label => label.Text,
            RichTextLabel rich => rich.Text,
            Button button => button.Text,
            _ => null,
        };

        if (!string.IsNullOrWhiteSpace(text)) {
            texts.Add(new JsonObject {
                ["name"] = node.Name.ToString(),
                ["text"] = text.Trim(),
            });
        }

        foreach (var child in node.GetChildren())
            Collect(child, texts, max);
    }
}
