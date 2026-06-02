# DevMode MCP (stdio proxy)

Stdio MCP server built with the official [Model Context Protocol C# SDK](https://csharp.sdk.modelcontextprotocol.io/). Forwards tool calls to the DevMode in-game HTTP bridge (`http://127.0.0.1:9877/messages`).

**Requires:** Slay the Spire 2 running with **DevMode** loaded (for tool execution; tool listing works without the game).

## Build

Windows:

```bash
dotnet publish tools/DevMode.Mcp/DevMode.Mcp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `tools/DevMode.Mcp/bin/Release/net8.0/win-x64/publish/DevMode.Mcp.exe`

macOS (Apple Silicon example):

```bash
dotnet publish tools/DevMode.Mcp/DevMode.Mcp.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

## Client configuration

Add or update the **`devmode`** entry under `mcpServers` (stdio). Keep your other MCP servers — only merge in the `devmode` block.

Build once:

```bash
dotnet build tools/DevMode.Mcp/DevMode.Mcp.csproj -c Release
```

Paste the `devmode` block from the repo README into your MCP client config. (relative to repo / workspace root; requires .NET 8):

```json
"devmode": {
  "command": "dotnet",
  "args": [
    "exec",
    "tools/DevMode.Mcp/bin/Release/net8.0/DevMode.Mcp.dll",
    "--",
    "--port",
    "9877"
  ]
}
```

**Optional launchers** (auto-build): Windows [`.cursor/run-devmode-mcp.bat`](../../.cursor/run-devmode-mcp.bat), macOS/Linux [`.cursor/run-devmode-mcp.sh`](../../.cursor/run-devmode-mcp.sh) (`chmod +x` once).

### Published executable

Windows:

```json
{
  "mcpServers": {
    "devmode": {
      "command": "C:/path/to/DevMode.Mcp.exe",
      "args": ["--port", "9877"]
    }
  }
}
```

macOS / Linux:

```json
{
  "mcpServers": {
    "devmode": {
      "command": "/path/to/DevMode.Mcp",
      "args": ["--port", "9877"]
    }
  }
}
```

Default port is **9877** (must match `McpConfig.Port` in the mod). Override with `--port` on the proxy only if you also change the mod source and rebuild.

## Tools

- `get_game_state`
- `combat_action`
- `map_action`
- `dev_get_session`
- `dev_list_save_slots`
- `dev_tag_save_slot`
- `dev_load_save_slot`
- `dev_start_test_run`
- `dev_list_cards`
- `dev_add_card`
- `dev_remove_card`
- `dev_list_monsters`
- `dev_list_enemies`
- `dev_add_monster`
- `dev_set_cheat`
- `dev_set_stat`

Health check: `GET http://127.0.0.1:9877/health`

Agent bootstrap: `make dev-session` (sync + launch + wait for bridge). See repo README **Agent debug loop**.

### Manual HTTP test (in-game bridge)

With the game running and DevMode loaded:

```bash
curl -s http://127.0.0.1:9877/health
```

```bash
curl -s -X POST http://127.0.0.1:9877/messages \
  -H "Content-Type: application/json" \
  -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"dev_get_session\",\"arguments\":{}}}"
```
