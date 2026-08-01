using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using KitLib.AI.Sts2.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.Mcp.Tools;

/// <summary>
/// Focuses (hovers) a UI element, so hover-driven behaviour can be exercised without a mouse.
/// </summary>
/// <remarks>
/// <para>
/// The bridge could CLICK things but never HOVER one, and a whole class of mod behaviour — every preview,
/// tooltip and forecast — exists only on hover. Those surfaces were mechanically untestable: an agent could
/// walk a route and take a reward, but never see what a mod claimed the reward WOULD be.
/// </para>
/// <para>
/// Focus is delivered by invoking the node's own <c>OnFocus</c> / <c>OnUnfocus</c> override, which is what the
/// game itself calls. Harmony rewrites method BODIES, so a reflected invoke runs every patch attached to them
/// exactly as a real mouse would — that is the property that makes this a faithful test and not a simulation.
/// The methods are protected and declared per node type, so the lookup walks the type chain rather than
/// assuming the level it sits at.
/// </para>
/// <para>
/// The previously focused node is unfocused first, because hover previews key their tip sets by owner and
/// several throw on a double-keyed owner. Call with <c>target: "none"</c> to clear without focusing anything.
/// </para>
/// </remarks>
internal sealed class DevHoverTool : IMcpTool {
    // What was focused last, so the next hover (or an explicit clear) can release it. Single-valued because
    // a mouse has one pointer: the game never has two hovered elements either.
    private static Node? _focused;

    public string Name => "dev_hover";

    public string Description =>
        "Hover (focus) a UI element so hover-only behaviour fires: map node, card-reward alternative button, "
        + "campfire option, or relic. Omit index to list what is hoverable. Use target 'none' to unhover.";

    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "target": {
                "type": "string",
                "description": "map_node, reward_alternative, rest_option, relic, or none (unhover)."
            },
            "index": {
                "type": "integer",
                "description": "Which one to hover, from the list this tool returns. Omit to only list them.",
                "default": -1
            },
            "click": {
                "type": "boolean",
                "description": "Also click it after focusing. Lets a hover preview be compared against what committing to it actually does.",
                "default": false
            }
        },
        "required": ["target"]
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) => Task.FromResult(Run(args));

    private static JsonNode Run(JsonObject args) {
        if (!args.TryGetPropertyValue("target", out var targetNode)
            || targetNode?.GetValueKind() != JsonValueKind.String) {
            return DevCardMcpHelper.Fail("Missing or invalid target.");
        }

        var target = targetNode.GetValue<string>()!.Trim().ToLowerInvariant();
        if (target == "none") {
            var cleared = Unfocus();
            return new JsonObject { ["ok"] = true, ["unfocused"] = cleared };
        }

        if (Engine.GetMainLoop() is not SceneTree tree)
            return DevCardMcpHelper.Fail("Scene tree unavailable.");

        List<Node>? candidates = Collect(target, tree.Root);
        if (candidates == null)
            return DevCardMcpHelper.Fail(
                $"Unknown target '{target}'. Use map_node, reward_alternative, rest_option, relic, or none.");

        var listed = new JsonArray();
        foreach (var node in candidates)
            listed.Add(Describe(node));

        var index = args.TryGetPropertyValue("index", out var indexNode)
            && indexNode?.GetValueKind() == JsonValueKind.Number
                ? indexNode.GetValue<int>()
                : -1;

        // No index is a LIST request, not an error — discovering what is on screen is the first half of
        // driving it, and the indices this returns are what the caller then hovers by.
        if (index < 0)
            return new JsonObject { ["ok"] = true, ["target"] = target, ["count"] = listed.Count, ["hoverable"] = listed };

        if (index >= candidates.Count)
            return DevCardMcpHelper.Fail($"index {index} out of range — {candidates.Count} '{target}' on screen.");

        Unfocus();
        var chosen = candidates[index];
        if (!Invoke(chosen, "OnFocus"))
            return DevCardMcpHelper.Fail($"{chosen.GetType().Name} has no OnFocus to invoke.");

        _focused = chosen;

        var clicked = false;
        if (args.TryGetPropertyValue("click", out var clickNode)
            && clickNode?.GetValueKind() == JsonValueKind.True) {
            if (chosen is not NClickableControl clickable)
                return DevCardMcpHelper.Fail($"{chosen.GetType().Name} is not clickable.");

            // ForceClick is the game's own programmatic press, so the button's real handler runs -- which is
            // what makes a hover preview comparable against actually committing to it.
            clickable.ForceClick();
            clicked = true;
            _focused = null;   // the node this focused is usually torn down by its own handler
        }

        return new JsonObject {
            ["ok"] = true,
            ["target"] = target,
            ["index"] = index,
            ["hovered"] = Describe(chosen),
            ["clicked"] = clicked,
            ["count"] = listed.Count,
        };
    }

    private static List<Node>? Collect(string target, Node root) => target switch {
        "map_node" => Widen(UIHelper.FindAll<NMapPoint>(root)),
        "reward_alternative" => Widen(UIHelper.FindAll<NCardRewardAlternativeButton>(root)),
        "rest_option" => Widen(UIHelper.FindAll<NRestSiteButton>(root)),
        "relic" => Widen(UIHelper.FindAll<NRelicInventoryHolder>(root)),
        _ => null,
    };

    private static List<Node> Widen<T>(List<T> nodes) where T : Node {
        var result = new List<Node>(nodes.Count);
        foreach (var node in nodes) {
            // A queue-freed node is still a child until the end of the frame; hovering one would key a tip
            // set to an owner about to vanish.
            if (GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
                result.Add(node);
        }
        return result;
    }

    private static JsonObject Describe(Node node) {
        var described = new JsonObject {
            ["type"] = node.GetType().Name,
            ["name"] = node.Name.ToString(),
        };

        // A map point's coordinates and room type are what a caller actually selects on, so surface them
        // rather than making them guess from scene order.
        if (node is NMapPoint point) {
            described["row"] = point.Point.coord.row;
            described["col"] = point.Point.coord.col;
            described["pointType"] = point.Point.PointType.ToString();
        }

        return described;
    }

    private static string? Unfocus() {
        if (_focused == null)
            return null;

        var name = _focused.GetType().Name;
        if (GodotObject.IsInstanceValid(_focused))
            Invoke(_focused, "OnUnfocus");

        _focused = null;
        return name;
    }

    // OnFocus/OnUnfocus are protected and declared on the concrete node type (or a base of it). GetMethod does
    // not return inherited NON-PUBLIC members, so walk the chain with DeclaredOnly instead of assuming a level.
    private static bool Invoke(Node node, string method) {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (var type = node.GetType(); type != null; type = type.BaseType) {
            var found = type.GetMethod(method, flags, null, System.Type.EmptyTypes, null);
            if (found == null)
                continue;

            found.Invoke(node, null);
            return true;
        }

        return false;
    }
}
