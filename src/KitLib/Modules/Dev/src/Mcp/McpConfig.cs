using System;
using KitLib.Settings;

namespace KitLib.Mcp;

internal static class McpConfig {
    private const int DefaultPort = 9877;

    /// <summary>
    /// HTTP port for the in-game MCP server. <c>KITLIB_MCP_PORT</c> overrides the persisted KitLib
    /// setting (<c>McpPort</c>), which defaults to 9877. Read at bridge start, so changes need a restart.
    /// </summary>
    public static int Port {
        get {
            var raw = Environment.GetEnvironmentVariable("KITLIB_MCP_PORT");
            if (int.TryParse(raw, out var envPort) && envPort is > 0 and < 65536)
                return envPort;

            var configured = SettingsStore.Current.McpPort;
            return configured is > 0 and < 65536 ? configured : DefaultPort;
        }
    }

    /// <summary>
    /// Optional bearer token. When set via <c>KITLIB_MCP_TOKEN</c>, the MCP and legacy endpoints
    /// require <c>Authorization: Bearer &lt;token&gt;</c>; <c>/health</c> stays open.
    /// </summary>
    public static string? Token {
        get {
            var raw = Environment.GetEnvironmentVariable("KITLIB_MCP_TOKEN");
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }
    }
}
