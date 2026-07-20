namespace KitLib.AI.Sts2.Mcp;

/// <summary>Per-call MCP play options set by <c>combat_action</c> before <see cref="Sts2ActionExecutor"/> runs.</summary>
internal static class McpPlayContext {
    public static string? SelectionCardId { get; set; }
    public static int? SelectionIndex { get; set; }

    public static void Clear() {
        SelectionCardId = null;
        SelectionIndex = null;
    }
}
