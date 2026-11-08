using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace KitLib.Mcp.Tools;

/// <summary>Per-tool MCP presentation metadata (annotations, title, Claude-specific hints).</summary>
internal sealed record McpToolMetadata(
    string Title,
    bool ReadOnlyHint = false,
    bool DestructiveHint = false,
    bool IdempotentHint = false,
    string? SearchHint = null,
    bool AlwaysLoad = false);

/// <summary>
/// Enriches <c>tools/list</c> entries with MCP <c>annotations</c>/<c>title</c>/<c>_meta</c> and owns
/// the "Core" tool profile (<c>/mcp-core</c>) used by token-sensitive clients.
/// Pure JSON handling only, so unit tests link this file without Godot.
/// </summary>
internal static class McpToolCatalog {
    /// <summary>Tools exposed on <c>/mcp-core</c>: enough to observe and drive normal play.</summary>
    public static readonly string[] CoreToolNames = [
        "get_game_state",
        "get_selection_state",
        "combat_action",
        "dev_combat_turn",
        "dev_wait_phase",
        "get_map_state",
        "map_action",
        "get_combat_stats",
        "dev_get_session",
        "dev_read_text",
    ];

    private static readonly HashSet<string> CoreTools = new(CoreToolNames, StringComparer.Ordinal);

    public static bool IsCore(string toolName) => CoreTools.Contains(toolName);

    private static readonly Dictionary<string, McpToolMetadata> ByName = new(StringComparer.Ordinal) {
        ["get_game_state"] = new("Get game state", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "game state hp gold combat hand enemy potion deck relics", AlwaysLoad: true),
        ["combat_action"] = new("Combat action", SearchHint: "play card end turn combat action"),
        ["get_selection_state"] = new("Get selection state", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "card selection pile pick discard draw exhaust"),
        ["selection_action"] = new("Selection action", SearchHint: "pick card selection confirm"),
        ["get_combat_stats"] = new("Get combat stats", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "combat stats damage events timeline"),
        ["map_action"] = new("Map action",
            SearchHint: "select map node card reward event shop rest proceed"),
        ["get_map_state"] = new("Get map state", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "map nodes route act available"),
        ["dev_get_session"] = new("Get dev session", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "run active phase save slot dev session", AlwaysLoad: true),
        ["dev_list_save_slots"] = new("List save slots", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "list save slots metadata"),
        ["dev_tag_save_slot"] = new("Tag save slot", SearchHint: "tag save slot note"),
        ["dev_load_save_slot"] = new("Load save slot", DestructiveHint: true,
            SearchHint: "load save slot run"),
        ["dev_start_test_run"] = new("Start test run", DestructiveHint: true,
            SearchHint: "start test run seed"),
        ["dev_list_cards"] = new("List cards", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "list card ids library"),
        ["dev_add_card"] = new("Add card", SearchHint: "add card deck"),
        ["dev_remove_card"] = new("Remove card", DestructiveHint: true,
            SearchHint: "remove card deck"),
        ["dev_add_relic"] = new("Add relic", SearchHint: "add relic"),
        ["dev_hover"] = new("Hover card or relic", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "hover preview card relic tooltip"),
        ["dev_list_monsters"] = new("List monsters", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "list monsters ids"),
        ["dev_list_enemies"] = new("List enemies", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "list enemies encounter combat"),
        ["dev_add_monster"] = new("Add monster", SearchHint: "add monster combat encounter"),
        ["dev_set_cheat"] = new("Set cheat", DestructiveHint: true,
            SearchHint: "cheat gold hp energy toggle run stats"),
        ["dev_set_stat"] = new("Set stat", DestructiveHint: true,
            SearchHint: "set hp gold energy stat"),
        ["dev_wait_phase"] = new("Wait for phase", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "wait phase play_phase not_combat poll"),
        ["dev_read_text"] = new("Read screen text", ReadOnlyHint: true, IdempotentHint: true,
            SearchHint: "read ui text labels hud button captions"),
        ["dev_combat_turn"] = new("Play combat turn",
            SearchHint: "combat turn play cards end turn"),
        ["dev_batch"] = new("Batch tool calls",
            SearchHint: "batch multiple tools one call"),
    };

    public static int MetadataCount => ByName.Count;

    /// <summary>Adds <c>title</c>, <c>annotations</c> and Claude <c>_meta</c> hints to one tool schema.</summary>
    public static void Enrich(JsonObject schema, string toolName) {
        if (!ByName.TryGetValue(toolName, out var meta))
            return;

        schema["title"] = meta.Title;

        var annotations = new JsonObject {
            ["title"] = meta.Title,
            ["openWorldHint"] = false,
        };
        if (meta.ReadOnlyHint)
            annotations["readOnlyHint"] = true;
        if (meta.DestructiveHint)
            annotations["destructiveHint"] = true;
        if (meta.IdempotentHint)
            annotations["idempotentHint"] = true;
        schema["annotations"] = annotations;

        var clientMeta = new JsonObject();
        if (meta.SearchHint is { Length: > 0 })
            clientMeta["anthropic/searchHint"] = meta.SearchHint;
        if (meta.AlwaysLoad)
            clientMeta["anthropic/alwaysLoad"] = true;
        if (clientMeta.Count > 0)
            schema["_meta"] = clientMeta;
    }
}
