using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using KitLib.Host;
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
/// several throw on a double-keyed owner. Other game-advancing tools call <see cref="ReleaseHover"/> before
/// they run, so a preview cannot outlive the action that changed the screen. Call with
/// <c>target: "none"</c> to clear without focusing anything.
/// </para>
/// </remarks>
internal sealed class DevHoverTool : IMcpTool {
    // What was focused last, so the next hover (or an explicit clear) can release it. Single-valued because
    // a mouse has one pointer: the game never has two hovered elements either.
    private static Node? _focused;

    private const int TooltipMaxEntries = 12;
    private const int TooltipMaxChars = 1200;

    public string Name => "dev_hover";

    public string Description =>
        "Focus/hover a UI element so hover-only previews and tooltips fire, and return the visible tip text. "
        + "target = alias (map_node, reward_alternative, rest_option, relic, boss_icon, event_option, potion, "
        + "card, creature, treasure_relic), ANY node type name (subclasses match their base type too), 'list' "
        + "to discover hoverables, or 'none' to unhover. Omit index to list instances; map points include "
        + "available/availableIndex matching get_game_state. Hover is released automatically before any "
        + "game-advancing tool, so re-hover before reading a preview again.";

    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "target": {
                "type": "string",
                "description": "Alias (map_node, reward_alternative, rest_option, relic, boss_icon, event_option, potion, card, creature, treasure_relic), any node type name (e.g. NTopBarDeckButton), 'list' to discover what is on screen, or 'none' to unhover."
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

    public Task<JsonNode> ExecuteAsync(JsonObject args) => RunAsync(args);

    /// <summary>Release the current hover. Called by game-advancing tools before they mutate the screen so a
    /// hover preview/tooltip cannot survive the action that invalidates it.</summary>
    internal static void ReleaseHover() => Unfocus();

    private static async Task<JsonNode> RunAsync(JsonObject args) {
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

        if (target == "list") {
            var types = ListHoverableTypes(tree.Root);
            return new JsonObject { ["ok"] = true, ["count"] = types.Count, ["hoverableTypes"] = types };
        }

        List<Node>? candidates = Collect(target, tree.Root);
        if (candidates == null)
            return DevCardMcpHelper.Fail(
                $"Unknown target '{target}'. Pass an alias, a node type name, or 'list' to see what is on screen.");

        // Map points carry their selection index so a caller can hand it straight to map_action.
        var availableMap = candidates.Any(n => n is NMapPoint) ? BuildAvailableMapIndex() : null;

        var listed = new JsonArray();
        foreach (var node in candidates)
            listed.Add(Describe(node, availableMap));

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

        // Let the preview/tooltip render before reading it, so `dev_batch` and single calls see the same text.
        await WaitFramesAsync(2);
        var tooltip = CollectTooltipText();

        var clicked = false;
        if (args.TryGetPropertyValue("click", out var clickNode)
            && clickNode?.GetValueKind() == JsonValueKind.True) {
            if (chosen is not NClickableControl clickable) {
                Unfocus();
                return DevCardMcpHelper.Fail($"{chosen.GetType().Name} is not clickable.");
            }

            // ForceClick is the game's own programmatic press, so the button's real handler runs -- which is
            // what makes a hover preview comparable against actually committing to it.
            clickable.ForceClick();
            clicked = true;
            await WaitFramesAsync(1);

            // Release hover even when the handler kept the node alive; otherwise the highlight would stick
            // with no way to clear it because _focused used to be dropped here.
            if (GodotObject.IsInstanceValid(chosen))
                Invoke(chosen, "OnUnfocus");
            _focused = null;
            tooltip = CollectTooltipText();
        }

        var response = new JsonObject {
            ["ok"] = true,
            ["target"] = target,
            ["index"] = index,
            ["hovered"] = Describe(chosen, availableMap),
            ["clicked"] = clicked,
            ["count"] = listed.Count,
        };
        if (tooltip != null)
            response["tooltip"] = tooltip;
        return response;
    }

    private static async Task WaitFramesAsync(int frames) {
        if (Engine.GetMainLoop() is not SceneTree tree)
            return;
        for (var i = 0; i < frames; i++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>Visible hover-tip text, so callers do not have to guess dev_read_text targets.</summary>
    private static JsonArray? CollectTooltipText() {
        if (Engine.GetMainLoop() is not SceneTree tree)
            return null;

        var texts = new JsonArray();
        CollectTipText(tree.Root, texts);
        return texts.Count > 0 ? texts : null;
    }

    private static void CollectTipText(Node node, JsonArray sink) {
        if (sink.Count >= TooltipMaxEntries)
            return;
        if (node is CanvasItem { Visible: false })
            return;

        var name = node.Name.ToString();
        var isTipRoot = name.Contains("HoverTip", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("Tooltip", StringComparison.OrdinalIgnoreCase);
        if (isTipRoot) {
            CollectLabelsUnder(node, sink);
            return;
        }

        foreach (var child in node.GetChildren())
            CollectTipText(child, sink);
    }

    private static void CollectLabelsUnder(Node node, JsonArray sink) {
        if (sink.Count >= TooltipMaxEntries)
            return;
        if (node is CanvasItem { Visible: false })
            return;

        var text = node switch {
            Label label => label.Text,
            RichTextLabel rich => rich.Text,
            Button button => button.Text,
            _ => null,
        };
        if (!string.IsNullOrWhiteSpace(text)) {
            var trimmed = text.Trim();
            sink.Add(new JsonObject {
                ["name"] = node.Name.ToString(),
                ["text"] = trimmed.Length > TooltipMaxChars ? trimmed[..TooltipMaxChars] : trimmed,
            });
        }

        foreach (var child in node.GetChildren())
            CollectLabelsUnder(child, sink);
    }

    /// <summary>Friendly names for the surfaces asked for most; any other type name works directly.</summary>
    private static readonly Dictionary<string, string> Aliases = new() {
        ["map_node"] = nameof(NMapPoint),
        ["reward_alternative"] = nameof(NCardRewardAlternativeButton),
        ["rest_option"] = nameof(NRestSiteButton),
        ["relic"] = nameof(NRelicInventoryHolder),
        ["boss_icon"] = "NTopBarBossIcon",
        ["event_option"] = "NEventOptionButton",
        ["potion"] = "NPotionHolder",
        ["card"] = "NCardHolder",
        ["creature"] = "NCreature",
        ["treasure_relic"] = "NTreasureRoomRelicHolder",
    };

    /// <remarks>
    /// Resolution is by TYPE NAME rather than a fixed list, because 124 node types implement
    /// <c>OnFocus()</c> and hard-coding a handful means a new surface needs a new build every time.
    /// Matching walks the base-type chain, so an alias (or type name) naming a base class also finds the
    /// concrete subclasses the game actually instantiates — e.g. <c>map_node</c> matches NNormalMapPoint,
    /// and <c>card</c> matches NHandCardHolder and NGridCardHolder.
    /// </remarks>
    private static List<Node>? Collect(string target, Node root) {
        var typeName = Aliases.TryGetValue(target, out var mapped) ? mapped : target;

        var matches = new List<Node>();
        CollectByTypeName(root, typeName, matches);
        return matches.Count > 0 || IsKnownTypeName(root, typeName) ? Widen(matches) : null;
    }

    private static void CollectByTypeName(Node node, string typeName, List<Node> found) {
        if (node is CanvasItem { Visible: false })
            return;

        if (MatchesTypeName(node, typeName))
            found.Add(node);

        foreach (var child in node.GetChildren())
            CollectByTypeName(child, typeName, found);
    }

    private static bool MatchesTypeName(Node node, string typeName) {
        for (var type = node.GetType(); type != null; type = type.BaseType) {
            if (string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase))
                return true;
            if (type == typeof(Node))
                break;
        }
        return false;
    }

    /// <summary>
    /// Distinguishes "no such surface on screen" (an empty list, which is a valid answer) from
    /// "you named something that is not a node type here" (an error worth reporting).
    /// </summary>
    private static bool IsKnownTypeName(Node root, string typeName) =>
        Aliases.ContainsValue(typeName) || typeName.StartsWith("n", StringComparison.OrdinalIgnoreCase);

    /// <summary>Every focusable type currently on screen, so a caller can discover targets.</summary>
    private static JsonArray ListHoverableTypes(Node root) {
        var counts = new Dictionary<string, int>();
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        CountFocusable(root, counts, aliases);

        var arr = new JsonArray();
        foreach (var pair in counts.OrderByDescending(p => p.Value)) {
            var entry = new JsonObject {
                ["type"] = pair.Key,
                ["count"] = pair.Value,
            };
            if (aliases.TryGetValue(pair.Key, out var alias))
                entry["alias"] = alias;
            arr.Add(entry);
        }
        return arr;
    }

    private static void CountFocusable(Node node, Dictionary<string, int> counts, Dictionary<string, string> aliases) {
        if (node is CanvasItem { Visible: false })
            return;

        // "Focusable" is exactly what this tool can drive: a type declaring OnFocus somewhere in its
        // chain. Anything listed here is a valid target value.
        if (node is Control && HasMethod(node, "OnFocus")) {
            var type = node.GetType();
            var name = type.Name;
            counts[name] = counts.TryGetValue(name, out var n) ? n + 1 : 1;
            if (!aliases.ContainsKey(name) && AliasForType(type) is { } alias)
                aliases[name] = alias;
        }

        foreach (var child in node.GetChildren())
            CountFocusable(child, counts, aliases);
    }

    private static string? AliasForType(Type type) {
        for (var current = type; current != null && current != typeof(Node); current = current.BaseType) {
            foreach (var pair in Aliases) {
                if (string.Equals(current.Name, pair.Value, StringComparison.OrdinalIgnoreCase))
                    return pair.Key;
            }
        }
        return null;
    }

    private static bool HasMethod(Node node, string method) {
        const BindingFlags flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (var type = node.GetType(); type != null; type = type.BaseType) {
            if (type.GetMethod(method, flags, null, System.Type.EmptyTypes, null) != null)
                return true;
        }
        return false;
    }

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

    /// <summary>Selection index per coordinate, mirroring get_game_state.mapNodes[].index.</summary>
    private static Dictionary<(int Row, int Col), int>? BuildAvailableMapIndex() {
        if (KitLibGameOps.Snapshot() is not { } snapshot || snapshot["mapNodes"] is not JsonArray nodes)
            return null;

        var map = new Dictionary<(int, int), int>();
        foreach (var entry in nodes) {
            if (entry is not JsonObject node)
                continue;
            var index = node["index"]?.GetValue<int>() ?? -1;
            var row = node["row"]?.GetValue<int>() ?? -1;
            var col = node["col"]?.GetValue<int>() ?? -1;
            if (index >= 0 && row >= 0 && col >= 0)
                map[(row, col)] = index;
        }
        return map;
    }

    private static JsonObject Describe(Node node, IReadOnlyDictionary<(int Row, int Col), int>? availableMap = null) {
        var described = new JsonObject {
            ["type"] = node.GetType().Name,
            ["name"] = node.Name.ToString(),
        };

        // A map point's coordinates and room type are what a caller actually selects on, so surface them
        // rather than making them guess from scene order. availableIndex is the same index
        // map_action select_map_node expects (get_game_state.mapNodes[].index).
        if (node is NMapPoint point) {
            var row = point.Point.coord.row;
            var col = point.Point.coord.col;
            described["row"] = row;
            described["col"] = col;
            described["pointType"] = point.Point.PointType.ToString();
            if (availableMap != null) {
                var available = availableMap.TryGetValue((row, col), out var actionIndex);
                described["available"] = available;
                if (available)
                    described["availableIndex"] = actionIndex;
            }
        }

        return described;
    }

    private static string? Unfocus() {
        if (_focused == null)
            return null;

        var name = _focused.GetType().Name;
        if (GodotObject.IsInstanceValid(_focused))
            Invoke((Node)_focused, "OnUnfocus");

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
