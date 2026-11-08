using System.Text.Json.Nodes;
using KitLib.Mcp;
using KitLib.Mcp.Tools;

namespace KitLib.DevTools.Tests;

public sealed class McpProtocolTests {
    [Theory]
    [InlineData("2026-07-28")]
    [InlineData("2025-11-25")]
    [InlineData("2025-06-18")]
    [InlineData("2025-03-26")]
    public void Negotiate_EchoesSupportedRevision(string version) {
        Assert.Equal(version, McpProtocol.Negotiate(version));
    }

    [Theory]
    [InlineData("2024-11-05")]
    [InlineData("2099-01-01")]
    [InlineData(null)]
    [InlineData("")]
    public void Negotiate_UnknownRevision_FallsBackToOldestSupported(string? requested) {
        Assert.Equal(McpProtocol.SupportedProtocolVersions[^1], McpProtocol.Negotiate(requested));
        Assert.Equal("2025-03-26", McpProtocol.Negotiate(requested));
    }

    [Fact]
    public void SessionStore_EvictsLeastRecentlyUsed() {
        var store = new McpSessionStore();
        for (var i = 0; i < 65; i++)
            store.Add($"s{i}");

        Assert.Equal(64, store.Count);
        Assert.False(store.Contains("s0"));
        Assert.True(store.Contains("s64"));
    }

    [Fact]
    public void SessionStore_TouchKeepsSessionAlive() {
        var store = new McpSessionStore();
        for (var i = 0; i < 64; i++)
            store.Add($"s{i}");

        store.Touch("s0");
        store.Add("new"); // evicts s1, the new least-recently-used session

        Assert.True(store.Contains("s0"));
        Assert.False(store.Contains("s1"));
        Assert.True(store.Contains("new"));
    }

    [Fact]
    public void SessionStore_AddExisting_MovesToFront() {
        var store = new McpSessionStore();
        store.Add("a");
        store.Add("b");
        store.Add("a");
        for (var i = 0; i < 62; i++)
            store.Add($"x{i}");

        Assert.True(store.Contains("a"));
        Assert.True(store.Contains("b"));
    }

    [Fact]
    public void Auth_IsDisabledWithoutConfiguredToken() {
        Assert.True(McpAuth.IsAuthorized(null, null));
        Assert.True(McpAuth.IsAuthorized("", "anything"));
    }

    [Theory]
    [InlineData("Bearer secret", true)]
    [InlineData("bearer secret", true)]
    [InlineData("Bearer  secret ", true)]
    [InlineData("Bearer wrong", false)]
    [InlineData("Basic secret", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Auth_RequiresMatchingBearerToken(string? header, bool expected) {
        Assert.Equal(expected, McpAuth.IsAuthorized("secret", header));
    }

    [Fact]
    public void Enrich_ReadOnlyTool_GetsAnnotationsAndClaudeHints() {
        var schema = new JsonObject { ["name"] = "get_game_state" };
        McpToolCatalog.Enrich(schema, "get_game_state");

        Assert.Equal("Get game state", schema["title"]!.GetValue<string>());
        var annotations = schema["annotations"]!.AsObject();
        Assert.Equal("Get game state", annotations["title"]!.GetValue<string>());
        Assert.True(annotations["readOnlyHint"]!.GetValue<bool>());
        Assert.False(annotations["openWorldHint"]!.GetValue<bool>());
        Assert.Null(annotations["destructiveHint"]);

        var meta = schema["_meta"]!.AsObject();
        Assert.True(meta["anthropic/alwaysLoad"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(meta["anthropic/searchHint"]!.GetValue<string>()));
    }

    [Fact]
    public void Enrich_DestructiveTool_MarksDestructive() {
        var schema = new JsonObject { ["name"] = "dev_load_save_slot" };
        McpToolCatalog.Enrich(schema, "dev_load_save_slot");

        var annotations = schema["annotations"]!.AsObject();
        Assert.True(annotations["destructiveHint"]!.GetValue<bool>());
        Assert.Null(annotations["readOnlyHint"]);

        var meta = schema["_meta"]!.AsObject();
        Assert.Null(meta["anthropic/alwaysLoad"]);
        Assert.False(string.IsNullOrWhiteSpace(meta["anthropic/searchHint"]!.GetValue<string>()));
    }

    [Fact]
    public void Enrich_UnknownTool_LeavesSchemaUntouched() {
        var schema = new JsonObject { ["name"] = "future_tool" };
        McpToolCatalog.Enrich(schema, "future_tool");

        Assert.Null(schema["title"]);
        Assert.Null(schema["annotations"]);
        Assert.Null(schema["_meta"]);
    }

    [Fact]
    public void CoreProfile_IsSubsetOfCatalog() {
        Assert.Equal(10, McpToolCatalog.CoreToolNames.Length);
        Assert.True(McpToolCatalog.IsCore("get_game_state"));
        Assert.True(McpToolCatalog.IsCore("dev_combat_turn"));
        Assert.False(McpToolCatalog.IsCore("dev_set_cheat"));
        Assert.False(McpToolCatalog.IsCore("dev_list_cards"));
    }

    [Fact]
    public void Catalog_MetadataCoversEveryTool() {
        Assert.Equal(26, McpToolCatalog.MetadataCount);
        foreach (var name in McpToolCatalog.CoreToolNames)
            Assert.True(McpToolCatalog.IsCore(name));
    }
}
